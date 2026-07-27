// -----------------------------------------------------------------------
// <copyright file="AITLargeTextureExternalizer.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Large Texture Streaming (build-time externalizer)
// </copyright>
// -----------------------------------------------------------------------
//
// 빌드 직전, 비-부팅 대형 Texture2D 를 초기 .data 밖(StreamingAssets)으로 빼내고
// 프로젝트 내 소스는 "동일 차원 단색 스텁"으로 치환한다. 런타임(AITStreamingTexture)이
// 매니페스트를 보고 first-frame 이후 실 텍스처를 스트리밍 로드하여 동일 Texture2D 객체에
// 픽셀을 제자리(in-place) 복원한다 — 이를 참조하는 모든 Sprite/Material 이 참조 재할당 없이 갱신.
//
// 메커니즘(M2, 차원 보존 스텁 + in-place 복원): 오디오 스트리밍 패턴의 텍스처 버전.
//   오디오는 AudioSource.clip(단일 가변 프로퍼티) 재할당으로 핫스왑하지만,
//   텍스처는 Sprite.texture 가 read-only 라 참조 재할당이 불가하다. 대신 스텁을 "원본과
//   동일 차원"으로 만들어 Sprite rect/pivot/border 가 동일하게 bake 되게 하고, 런타임에
//   공유 Texture2D 객체의 픽셀만 교체하면(LoadImage+Apply) 모든 참조가 자동으로 새 픽셀을 렌더한다.
//
// 효과: 동일 차원 단색 스텁은 crunch+brotli 에서 차원과 무관하게 ~0 으로 수렴하므로,
//   .data 에서 비-부팅 텍스처 바이트가 사라져 초기 다운로드/TTFF 가 감소한다. 원본은
//   StreamingAssets/ait-stream-texture/<guid>.<ext> 로 동봉(온디맨드).
//
// 적대검증(4-에이전트)으로 확정된 빌드타임 필수 완화:
//   ① 런타임 LoadImage 는 crunch/non-readable 텍스처에 실패 → 스텁만 isReadable=true +
//      crunchedCompression=false + Uncompressed 로 reimport (스텁은 단색이라 그래도 brotli ~0).
//   ② NormalMap / linear(sRGB=false) 는 LoadImage 가 sRGB 로 덮어써 깨짐 → 하드 제외.
//   ③ 부팅 씬 의존(GetDependencies scene0) + /Resources/ + Splash + 사용자 제외목록 → frame-1 보호.
//   ④ <minBytes(기본 512KB) 소형 텍스처 제외 (동적 Resources.Load 로 부팅에 끌려올 수 있는 작은 아이콘 보호).
//   ⑤ SpriteAtlas 패킹 대상 제외: 아틀라스는 빌드 시 소스 PNG 를 페이지(sactx-*)로
//      굽고 소스 자체는 .data 에 안 넣는다 → 소스 스텁은 (a) 아틀라스에 스텁을 패킹해 영구 투명,
//      (b) .data 의 페이지가 잔존해 on-wire 이득 0. 페이지 단위 defer 는 소스-PNG 스텁이 닿지 못하는
//      별도 메커니즘 영역이므로 여기서는 하드 제외(BuildSpriteAtlasPackedSet, 폴더 패킹 포함).
//
// 비파괴: 소스 원본은 <src>.aittexstreambak, .meta 는 <meta>.aittexstreammetabak 로 백업,
//   빌드 종료(성공/실패 무관) 시 둘 다 원상 복원한다. 비정상 종료 시 다음 에디터 로드의
//   안전망(SafetyNetRestore, 마커 게이트)이 잔존 백업을 자동 복원한다.
//
// 통합: AITConvertCore.BuildWebGL 가 빌드 직전 ExternalizeForBuild 를 호출하고,
//   finally 에서 RestoreForBuild 로 원상 복원한다.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AppsInToss.Editor
{
    /// <summary>
    /// 빌드 단계 대형 텍스처 외부화/복원 처리기. <see cref="AITEditorScriptObject.textureStreaming"/>
    /// 설정에 따라 동작하며, 런타임 컴포넌트 <c>AppsInToss.AITStreamingTexture</c> 와 짝을 이룬다.
    /// </summary>
    [InitializeOnLoad]
    public static class AITLargeTextureExternalizer
    {
        /// <summary>외부화된 텍스처/매니페스트가 놓이는 프로젝트 상대 경로.</summary>
        private const string StreamRootAssets = "Assets/StreamingAssets/ait-stream-texture";

        /// <summary>치환 전 원본 소스(png/jpg 등)를 보관하는 백업 접미사.</summary>
        private const string SrcBackupSuffix = ".aittexstreambak";

        /// <summary>치환 전 원본 .meta 를 보관하는 백업 접미사(스텁 임포터 override 복원용).</summary>
        private const string MetaBackupSuffix = ".aittexstreammetabak";

        /// <summary>apply 진행 중임을 표시하는 마커(Unity 가 무시하는 '.' 접두 숨김 파일).</summary>
        private const string MarkerRelative = "Assets/.ait-texstream-active";

        /// <summary>외부화 기본 최소 소스 크기(512KB). 동적 Resources.Load 로 부팅에 끌려올 수 있는 소형 아이콘 보호.</summary>
        private const long DefaultMinBytes = 524288;

        /// <summary>스트림 다운스케일 캡 하한. 이 값 미만의 캡은 사고 방지를 위해 무시(다운스케일 skip).</summary>
        private const int MinDownscaleSize = 16;

        /// <summary>한 번의 외부화 결과 핸들. finally 에서 정확한 복원에 사용.</summary>
        public sealed class TextureStreamHandle
        {
            /// <summary>이번 빌드에서 외부화가 실제로 수행되었는지.</summary>
            public bool Active;

            /// <summary>외부화된 텍스처 개수.</summary>
            public int Count;

            /// <summary>외부화된 총 바이트(소스 파일 크기 합계).</summary>
            public long TotalBytes;

            /// <summary>부팅 씬 의존으로 제외된 개수.</summary>
            public int ExcludedBoot;

            /// <summary>/Resources/ 경로로 제외된 개수.</summary>
            public int ExcludedResources;

            /// <summary>SpriteAtlas 패킹 대상으로 제외된 개수.</summary>
            public int ExcludedAtlas;

            /// <summary>동명·동차원 충돌로 제외된 개수(매칭 모호).</summary>
            public int ExcludedDuplicate;

            /// <summary>linear(sRGB=false) 또는 NormalMap 으로 제외된 개수.</summary>
            public int ExcludedColorSpace;
        }

        static AITLargeTextureExternalizer()
        {
            // 에디터 로드 시 안전망: 이전 빌드가 비정상 종료되어 복원이 누락된 경우(마커 잔존) 자동 복원.
            EditorApplication.delayCall += SafetyNetRestore;
        }

        /// <summary>
        /// 빌드 직전 호출: 설정이 켜져 있으면 비-부팅 대형 텍스처를 외부화하고 소스를 동일 차원 스텁으로 치환한다.
        /// </summary>
        /// <param name="config">프로젝트 에디터 설정. null 이거나 기능이 꺼져 있으면 no-op.</param>
        /// <returns>복원에 사용할 핸들(항상 non-null).</returns>
        public static TextureStreamHandle ExternalizeForBuild(AITEditorScriptObject config)
        {
            var handle = new TextureStreamHandle();
            // tri-state: -1 = 자동(기본 활성), 0 = 비활성, 1 = 강제 활성.
            bool enabled = config != null && (config.textureStreaming == 1
                || (config.textureStreaming < 0 && AITDefaultSettings.GetDefaultTextureStreaming()));
            if (config == null || !enabled)
            {
                return handle;
            }

            try
            {
                long minBytes = config.textureStreamingMinBytes > 0 ? config.textureStreamingMinBytes : DefaultMinBytes;
                string[] dirs = SplitDirs(config.textureStreamingDirs);              // null = 전체 Assets
                string[] excludeDirs = SplitDirs(config.textureStreamingExcludeDirs); // 사용자 escape hatch

                string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                var bootSet = BuildBootDependencySet();

                // SpriteAtlas 패킹 집합: 예외 발생 시 전체 외부화 no-op(부분 적용 → 영구 투명 스프라이트 리스크 차단).
                HashSet<string> atlasPaths;
                string[] atlasFolders;
                try
                {
                    (atlasPaths, atlasFolders) = BuildSpriteAtlasPackedSet();
                }
                catch (Exception atlasEx)
                {
                    Debug.LogWarning($"[AIT-StreamingTexture] SpriteAtlas 패킹 집합 산출 실패 → 외부화 전체 skip(영구 투명 스프라이트 리스크 차단): {atlasEx.Message}");
                    return handle; // no-op: 비활성 핸들 반환
                }

                CreateMarker();
                Directory.CreateDirectory(Path.Combine(projectRoot, StreamRootAssets));

                // 제외 사유별 카운터(빌드 리포트용).
                int cntBoot = 0, cntResources = 0, cntAtlas = 0, cntDuplicate = 0, cntColorSpace = 0;

                // ─── 1단계: 후보 수집 + 차원 산출 ─────────────────────────────────
                // (동명·동차원 충돌 검사를 위해 전체 목록을 먼저 모은다.)
                var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" });

                // 경로 → (guid, w, h) 후보 목록(제외 게이트 통과 + minBytes 충족).
                var candidates = new List<(string guid, string path, int w, int h, long size)>();

                foreach (var g in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(g);
                    if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/"))
                    {
                        continue;
                    }

                    if (path.StartsWith(StreamRootAssets))
                    {
                        continue; // 자기 사본 제외
                    }

                    if (dirs != null && !UnderAny(path, dirs))
                    {
                        continue;
                    }

                    // 제외 게이트: 사유별 카운터 갱신.
                    if (!PassesExclusionGatesWithCount(path, bootSet, atlasPaths, atlasFolders, excludeDirs,
                            ref cntBoot, ref cntResources, ref cntAtlas, ref cntColorSpace, out _))
                    {
                        continue;
                    }

                    string srcFull = Path.Combine(projectRoot, path);
                    if (!File.Exists(srcFull))
                    {
                        continue;
                    }

                    long size = new FileInfo(srcFull).Length;
                    if (size < minBytes)
                    {
                        continue;
                    }

                    // 스텁 차원 = "임포트된" 텍스처 차원(crunch 의 maxTextureSize 캡 반영 후). 이래야
                    // 스텁 reimport 시 Sprite rect/pivot/border 가 crunch 가 만든 것과 동일하게 bake 된다.
                    var imported = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    int w = imported != null ? imported.width : 0;
                    int h = imported != null ? imported.height : 0;
                    if (w <= 0 || h <= 0)
                    {
                        Debug.LogWarning($"[AIT-StreamingTexture] 차원 산출 실패(skip): {path}");
                        continue;
                    }

                    candidates.Add((g, path, w, h, size));
                }

                // ─── 2단계: 동명·동차원 충돌 검사 ────────────────────────────────
                // (name, w, h) 키가 동일한 Texture2D 가 2개 이상이면 런타임 매칭이 모호 → 그룹 전체 제외.
                var nameKey = new Dictionary<string, List<int>>(); // key → candidate 인덱스 목록
                for (int i = 0; i < candidates.Count; i++)
                {
                    var (_, cpath, cw, ch, _) = candidates[i];
                    string name = Path.GetFileNameWithoutExtension(cpath);
                    string key = name + "\0" + cw + "\0" + ch;
                    if (!nameKey.TryGetValue(key, out var idxList))
                    {
                        nameKey[key] = idxList = new List<int>();
                    }

                    idxList.Add(i);
                }

                var duplicateIndices = new HashSet<int>();
                foreach (var kv in nameKey)
                {
                    if (kv.Value.Count >= 2)
                    {
                        var parts = kv.Key.Split('\0');
                        string dupName = parts[0];
                        string dupW = parts.Length > 1 ? parts[1] : "?";
                        string dupH = parts.Length > 2 ? parts[2] : "?";
                        var paths = new List<string>();
                        foreach (var idx in kv.Value)
                        {
                            duplicateIndices.Add(idx);
                            paths.Add(candidates[idx].path);
                        }

                        Debug.LogWarning($"[AIT-StreamingTexture] 동명·동차원 텍스처 {kv.Value.Count}건 — 매칭 모호로 외부화에서 제외: {dupName} ({dupW}x{dupH})\n  {string.Join("\n  ", paths)}");
                        cntDuplicate += kv.Value.Count;
                    }
                }

                // ─── 3단계: 외부화 실행(스텁 치환 + 스트리밍 소스 복사) ──────────────
                //    엔트리 문자열은 4단계의 brotli 채택 판정 후 확정하므로, 여기서는 레코드만 수집.
                var records = new List<(string g, string streamFile, string texName, int w, int h, long size)>();
                int n = 0;
                long stubbedBytes = 0;
                var detailLines = new List<string>();

                for (int i = 0; i < candidates.Count; i++)
                {
                    if (duplicateIndices.Contains(i))
                    {
                        continue; // 동명·동차원 충돌 → 제외
                    }

                    var (g, path, w, h, size) = candidates[i];
                    string srcFull = Path.Combine(projectRoot, path);
                    string ext = Path.GetExtension(path).ToLowerInvariant(); // ".png" 등(점 포함)
                    string texName = Path.GetFileNameWithoutExtension(path);

                    // 1) 원본 소스 → StreamingAssets/<guid><ext> (온디맨드 스트리밍 소스).
                    //    png/jpg 만 런타임 LoadImage 가능 — 그 외 확장자는 매니페스트에 기록은 하되
                    //    런타임이 처리 못하면 조용히 스킵(측정/대형 텍스처는 전부 png).
                    string streamFile = g + ext;
                    File.Copy(srcFull, Path.Combine(projectRoot, StreamRootAssets, streamFile), true);

                    // 2) 소스 + .meta 백업(둘 다 — 스텁이 소스 바이트와 임포터 설정 모두 바꾸므로).
                    string metaFull = srcFull + ".meta";
                    string srcBak = srcFull + SrcBackupSuffix;
                    string metaBak = metaFull + MetaBackupSuffix;
                    if (!File.Exists(srcBak))
                    {
                        File.Copy(srcFull, srcBak, true);
                    }

                    if (File.Exists(metaFull) && !File.Exists(metaBak))
                    {
                        File.Copy(metaFull, metaBak, true);
                    }

                    // 3) 동일 차원 단색 스텁 PNG 를 소스에 덮어쓴다(leak-safe: temp Texture2D 는 finally 에서 파괴).
                    if (!WriteStubPng(srcFull, w, h))
                    {
                        // 스텁 생성 실패 → 이 텍스처는 백업 되돌리고 건너뜀.
                        RevertSingle(srcFull, metaFull, srcBak, metaBak, projectRoot);
                        // 스트리밍 소스 복사본도 정리(매니페스트에 안 실릴 고아 파일 방지).
                        SafeDelete(Path.Combine(projectRoot, StreamRootAssets, streamFile));
                        continue;
                    }

                    // 4) 스텁 서브셋만 런타임 writable 로 reimport: isReadable + non-crunch + Uncompressed.
                    //    sprite mode/spritesheet/pivot/border/sRGB 등은 건드리지 않아 Sprite rect 동일 유지.
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                    var ti2 = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (ti2 != null)
                    {
                        ti2.isReadable = true;
                        ti2.crunchedCompression = false;
                        ti2.textureCompression = TextureImporterCompression.Uncompressed;
                        ti2.SaveAndReimport();
                    }

                    records.Add((g, streamFile, texName, w, h, size));
                    n++;
                    stubbedBytes += size;
                    detailLines.Add($"  {texName} ({w}x{h}, {size / 1048576f:0.00}MB) → {streamFile}");
                }

                // ─── 3-1단계: 스트림 사본 다운스케일(HiDPI 캡, lossy, 기본 ON) ───────────
                //    스트림 사본(StreamingAssets, CDN 배포본)만 축소한다. 프로젝트 원본은 이미 스텁으로
                //    치환됐고(백업으로 복원됨), 스텁은 '원본 차원' 유지라 Sprite rect bake 계약은 불변.
                //    균일 배율(캡÷최대변)로 축소해 스프라이트시트 서브-rect UV 비율도 보존한다.
                //    반드시 brotli 앞에서 실행해야 이미 줄어든 바이트를 brotli 가 다시 압축한다.
                //    downscaledDims: streamFile → (sw, sh). 매니페스트 sw/sh 로 기록되어 런타임 기대 차원이 됨.
                var downscaledDims = new Dictionary<string, (int w, int h)>();
                bool dsEnabled = config.textureStreamDownscale == 1
                    || (config.textureStreamDownscale < 0 && AITDefaultSettings.GetDefaultTextureStreamDownscale());
                if (dsEnabled && records.Count > 0)
                {
                    int dsCap = ResolveDownscaleCap(config);
                    if (dsCap < MinDownscaleSize)
                    {
                        Debug.LogWarning($"[AIT-StreamingTexture] textureStreamDownscaleMaxSize={dsCap} < {MinDownscaleSize} → 다운스케일 skip.");
                    }
                    else
                    {
                        int dsCount = 0;
                        long dsBefore = 0, dsAfter = 0;
                        foreach (var rec in records)
                        {
                            // 주의: rec.w/rec.h 로 pre-filter 하지 않는다. 이 값은 '임포트 후' 치수
                            // (클램프/임포터 maxTextureSize 캡 반영)라 스트림 사본(raw 소스)의 실 픽셀
                            // 치수보다 작을 수 있고, 그걸로 걸러내면 "임포트는 캡됐지만 소스는 캡 초과"인
                            // 주 대상(예: 4096 소스 + 기본 클램프 2048)이 기본 posture 에서 전부 스킵된다.
                            // 실 판정은 TryDownscaleStreamImage 가 스트림 파일을 디코드한 실제 치수로
                            // 수행하며, 캡 이하면 파일을 건드리지 않고 false 를 반환한다.
                            string absStream = Path.Combine(projectRoot, StreamRootAssets, rec.streamFile);
                            if (TryDownscaleStreamImage(absStream, dsCap, out int nw, out int nh, out long bBefore, out long bAfter))
                            {
                                downscaledDims[rec.streamFile] = (nw, nh);
                                dsCount++;
                                dsBefore += bBefore;
                                dsAfter += bAfter;
                                Debug.Log($"[AIT-StreamingTexture]   다운스케일: {rec.texName} {rec.w}x{rec.h} → {nw}x{nh} ({bBefore / 1048576f:0.00}→{bAfter / 1048576f:0.00}MB)");
                            }
                        }

                        if (dsCount > 0)
                        {
                            Debug.Log($"[AIT-StreamingTexture] 스트림 다운스케일 {dsCount}개(캡 {dsCap}px, HiDPI 헤드룸), {dsBefore / 1048576f:0.00}→{dsAfter / 1048576f:0.00}MB. (표시 해상도 lossy — 프로젝트 원본 불변)");
                        }
                    }
                }

                // ─── 3-1.4단계: 불투명 스트림 PNG → JPEG 전환(lossy, 시각 검증 전 기본 OFF) ───
                //    다운스케일(3-1) 뒤·무손실 재압축(3-1.5) 앞 — 전환된 파일은 .jpg 로 개명되어
                //    oxipng 대상에서 자연 제외된다. 개명 시 레코드와 다운스케일 차원 맵의 키를
                //    함께 이관해야 매니페스트가 새 파일명을 가리킨다.
                if (records.Count > 0)
                {
                    var jpegCandidates = new List<string>();
                    foreach (var rec in records)
                    {
                        jpegCandidates.Add(Path.Combine(projectRoot, StreamRootAssets, rec.streamFile));
                    }

                    var jpegRenamed = AITTextureStreamJpegTranscoder.TranscodeInPlace(config, jpegCandidates);
                    if (jpegRenamed.Count > 0)
                    {
                        for (int ri = 0; ri < records.Count; ri++)
                        {
                            var rec = records[ri];
                            string abs = Path.Combine(projectRoot, StreamRootAssets, rec.streamFile);
                            if (!jpegRenamed.TryGetValue(abs, out string newAbs))
                            {
                                continue;
                            }

                            string newFile = Path.GetFileName(newAbs);
                            if (downscaledDims.TryGetValue(rec.streamFile, out var dsDim))
                            {
                                downscaledDims.Remove(rec.streamFile);
                                downscaledDims[newFile] = dsDim;
                            }

                            records[ri] = (rec.g, newFile, rec.texName, rec.w, rec.h, rec.size);
                        }
                    }
                }

                // ─── 3-1.5단계: 스트림 PNG 사본 무손실 재압축(oxipng, 기본 ON) ─────────
                //    다운스케일이 EncodeToPNG 로 다시 쓴 무최적화 deflate 와 원본 소스 PNG 를
                //    함께 누른다. 픽셀 불변(무손실) — brotli(3-2) 앞에서 실행해야 br 판정이
                //    이미 줄어든 바이트를 기준으로 이뤄진다.
                if (records.Count > 0)
                {
                    var pngPaths = new List<string>();
                    foreach (var rec in records)
                    {
                        pngPaths.Add(Path.Combine(projectRoot, StreamRootAssets, rec.streamFile));
                    }

                    AITTextureStreamRecompressor.RecompressInPlace(config, pngPaths);
                }

                // ─── 3-2단계: 스트리밍 소스 brotli(.br) 인코딩 ────────────────────────
                //    <guid><ext> 는 아직 원본 바이트(스텁 치환은 프로젝트 내 소스에만 적용, 복사본은
                //    복사 시점의 원본). PNG/JPG 는 이미 엔트로피 코딩돼 이득이 파일별 0~18.5%로 편차가
                //    커서, ShouldKeep(≥10%) 채택 파일만 <guid><ext>.br 로 대체하고 원본은 제거한다.
                //    런타임 AITStreamingCodec 이 encoding="br" 를 매직 스니핑으로 해제(서버 해제 포함).
                var texBr = new Dictionary<string, string>(); // streamFile → streamFile+".br"
                if (records.Count > 0 && AITBrotliCompressor.TryResolveNode(out _))
                {
                    var brSources = new List<string>();
                    foreach (var rec in records)
                    {
                        brSources.Add(Path.Combine(projectRoot, StreamRootAssets, rec.streamFile));
                    }

                    var brResults = AITBrotliCompressor.Compress(brSources);
                    foreach (var rec in records)
                    {
                        string abs = Path.Combine(projectRoot, StreamRootAssets, rec.streamFile);
                        if (brResults.TryGetValue(abs, out var r) && r.Ok
                            && AITBrotliCompressor.ShouldKeep(r.raw, r.br, AITBrotliCompressor.DefaultMinGainPercent))
                        {
                            SafeDelete(abs);            // 원본 제거 — .br 만 스트리밍.
                            SafeDelete(abs + ".meta");  // Refresh 전이라 대개 없음(방어적).
                            texBr[rec.streamFile] = rec.streamFile + ".br";
                        }
                        else
                        {
                            SafeDelete(abs + ".br");    // 이득 미달/실패 → 무압축 원본 유지.
                        }
                    }
                }

                var entries = new List<string>();
                foreach (var rec in records)
                {
                    bool isBr = texBr.TryGetValue(rec.streamFile, out string fileName);
                    if (!isBr)
                    {
                        fileName = rec.streamFile;
                    }

                    // width/height 는 스텁=원본 차원(FindStub 매칭 계약, 절대 축소본으로 바꾸지 않음).
                    // 다운스케일된 경우에만 sw/sh(스트림 실제 차원)를 추가 — 런타임이 LoadImage 후 기대 차원으로 사용.
                    bool isDs = downscaledDims.TryGetValue(rec.streamFile, out var dsd);
                    entries.Add("{\"guid\":\"" + rec.g + "\",\"name\":" + JsonStr(rec.texName)
                                + ",\"file\":" + JsonStr(fileName)
                                + (isBr ? ",\"encoding\":\"br\"" : string.Empty)
                                + ",\"width\":" + rec.w + ",\"height\":" + rec.h
                                + (isDs ? ",\"sw\":" + dsd.w + ",\"sh\":" + dsd.h : string.Empty) + "}");
                }

                // ─── 4단계: 매니페스트 동봉 ─────────────────────────────────────
                // 런타임 AITStreamingTexture 가 읽는 계약: maxConcurrent + entries.
                int maxConcurrent = config.textureStreamingMaxConcurrent > 0 ? config.textureStreamingMaxConcurrent : 3;
                var sb = new StringBuilder();
                sb.Append("{\"maxConcurrent\":").Append(maxConcurrent)
                  .Append(",\"entries\":[").Append(string.Join(",", entries)).Append("]}");
                File.WriteAllText(Path.Combine(projectRoot, StreamRootAssets, "manifest.json"), sb.ToString());
                AssetDatabase.Refresh();

                handle.Active = n > 0;
                handle.Count = n;
                handle.TotalBytes = stubbedBytes;
                handle.ExcludedBoot = cntBoot;
                handle.ExcludedResources = cntResources;
                handle.ExcludedAtlas = cntAtlas;
                handle.ExcludedDuplicate = cntDuplicate;
                handle.ExcludedColorSpace = cntColorSpace;

                if (!handle.Active)
                {
                    RemoveStreamRoot();
                    RemoveMarker();
                }

                // ─── 5단계: 빌드 리포트 ──────────────────────────────────────────
                if (n == 0)
                {
                    Debug.Log("[AIT-StreamingTexture] 텍스처 스트리밍: 외부화 대상 없음.");
                }
                else
                {
                    // 요약 1줄 (제외 사유별 카운트 포함)
                    var excParts = new List<string>();
                    if (cntBoot > 0) excParts.Add($"부팅 의존 {cntBoot}");
                    if (cntResources > 0) excParts.Add($"Resources {cntResources}");
                    if (cntAtlas > 0) excParts.Add($"아틀라스 {cntAtlas}");
                    if (cntDuplicate > 0) excParts.Add($"동명 충돌 {cntDuplicate}");
                    if (cntColorSpace > 0) excParts.Add($"linear/NormalMap {cntColorSpace}");
                    string excSummary = excParts.Count > 0 ? $" | 제외: {string.Join(", ", excParts)}" : "";
                    Debug.Log($"[AIT-StreamingTexture] ✓ 외부화 {n}개 / {stubbedBytes / 1048576f:0.0}MB 절감{excSummary}");

                    // 상세 목록
                    Debug.Log($"[AIT-StreamingTexture] 외부화 상세 목록:\n{string.Join("\n", detailLines)}");
                }

                return handle;
            }
            catch (Exception e)
            {
                // 외부화 실패가 빌드 전체를 막지 않도록: 부분 변경을 즉시 복원하고 비활성 핸들 반환.
                Debug.LogError($"[AIT-StreamingTexture] 외부화 예외 → 복원 후 건너뜀: {e}");
                RestoreAllBackups();
                RemoveStreamRoot();
                RemoveMarker();
                AssetDatabase.Refresh();
                return new TextureStreamHandle();
            }
        }

        /// <summary>
        /// 빌드 종료 후(성공/실패 무관) 호출: 스텁/임포터 override 를 원본으로 복원하고 StreamingAssets 사본을 제거한다.
        /// </summary>
        public static void RestoreForBuild(TextureStreamHandle handle)
        {
            if (handle == null || !handle.Active)
            {
                return;
            }

            try
            {
                int restored = RestoreAllBackups();
                RemoveStreamRoot();
                RemoveMarker();
                AssetDatabase.Refresh();
                Debug.Log($"[AIT-StreamingTexture] 복원 완료: {restored}개 소스/임포터 원상, StreamingAssets 사본 제거");
            }
            catch (Exception e)
            {
                Debug.LogError($"[AIT-StreamingTexture] 복원 예외: {e}");
            }
        }

        // ─────────────────────────── 제외 게이트 ───────────────────────────

        /// <summary>부팅 씬(0번)이 의존하는 에셋 경로 집합. 여기 포함된 텍스처는 절대 스텁하지 않는다(frame-1 보호).</summary>
        private static HashSet<string> BuildBootDependencySet()
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var scenes = EditorBuildSettings.scenes;
                if (scenes != null && scenes.Length > 0 && !string.IsNullOrEmpty(scenes[0].path))
                {
                    // AssetDatabase.GetDependencies(path, recursive) — 부팅 씬이 끌어오는 모든 에셋 경로(재귀).
                    foreach (var dep in AssetDatabase.GetDependencies(scenes[0].path, true))
                    {
                        set.Add(dep);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT-StreamingTexture] 부팅 의존성 산출 경고(보수적으로 모두 보호): {e.Message}");
            }

            return set;
        }

        /// <summary>
        /// 어느 SpriteAtlas 에든 패킹되는 텍스처/스프라이트의 소스 경로 집합 + 폴더 프리픽스.
        /// 아틀라스 패킹 대상은 빌드 시 아틀라스 페이지(<c>sactx-*</c>)로 구워져 .data 에 들어가고
        /// 소스 PNG 자체는 .data 에 포함되지 않는다. 따라서 소스를 단색 스텁으로 치환하면
        ///   ① 아틀라스가 그 스텁을 패킹해 영구 투명 스프라이트가 되고(시각 파손),
        ///   ② 정작 .data 의 아틀라스 페이지는 그대로 남아 on-wire 이득이 0 이다.
        /// → 소스-PNG 스텁 경로에서는 하드 제외한다(페이지 단위 defer 는 별도 메커니즘 영역).
        /// </summary>
        private static (HashSet<string> paths, string[] folders) BuildSpriteAtlasPackedSet()
        {
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var folders = new List<string>();
            // 예외는 잡지 않는다: 호출자(ExternalizeForBuild)가 전체 외부화 no-op 으로 처리.
            // (부분 적용 후 영구 투명 스프라이트 리스크를 원천 차단)
            foreach (var g in AssetDatabase.FindAssets("t:SpriteAtlas"))
            {
                string atlasPath = AssetDatabase.GUIDToAssetPath(g);
                var atlas = AssetDatabase.LoadAssetAtPath<UnityEngine.U2D.SpriteAtlas>(atlasPath);
                if (atlas == null)
                {
                    continue;
                }

                foreach (var packable in UnityEditor.U2D.SpriteAtlasExtensions.GetPackables(atlas))
                {
                    if (packable == null)
                    {
                        continue;
                    }

                    string p = AssetDatabase.GetAssetPath(packable);
                    if (string.IsNullOrEmpty(p))
                    {
                        continue;
                    }

                    // 폴더 패킹 → 폴더 하위 모든 스프라이트가 아틀라스에 들어감.
                    if (AssetDatabase.IsValidFolder(p))
                    {
                        folders.Add(p);
                    }
                    else
                    {
                        paths.Add(p);
                    }
                }
            }

            return (paths, folders.ToArray());
        }

        /// <summary>
        /// 적대검증으로 확정된 제외 필터를 모두 통과하는지 판정. 통과 시 <paramref name="ti"/> 에 임포터를 채운다.
        /// 제외 사유별 카운터(<paramref name="cntBoot"/> 등)를 갱신한다(빌드 리포트용).
        /// </summary>
        private static bool PassesExclusionGatesWithCount(
            string path,
            HashSet<string> bootSet,
            HashSet<string> atlasPaths,
            string[] atlasFolders,
            string[] excludeDirs,
            ref int cntBoot,
            ref int cntResources,
            ref int cntAtlas,
            ref int cntColorSpace,
            out TextureImporter ti)
        {
            ti = null;

            // ③ 부팅 씬 의존 → frame-1 에 보임 → 절대 스텁 금지.
            if (bootSet.Contains(path))
            {
                cntBoot++;
                return false;
            }

            // ① SpriteAtlas 패킹 대상 → 소스 스텁이 시각 파손 + on-wire 이득 0(아틀라스 페이지가 .data 에 잔존) → 하드 제외.
            if (atlasPaths.Contains(path) || (atlasFolders.Length > 0 && UnderAny(path, atlasFolders)))
            {
                cntAtlas++;
                return false;
            }

            // ③ Resources/ → 런타임 문자열 경로 Resources.Load 로 언제든 끌려옴(GetDependencies 가 추적 못함) → 보호.
            if (path.IndexOf("/Resources/", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                cntResources++;
                return false;
            }

            // ③ 스플래시/로고 휴리스틱 → 보호.
            if (path.IndexOf("Splash", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                cntBoot++; // 부팅 계열로 집계
                return false;
            }

            // ③ 사용자 escape hatch.
            if (excludeDirs != null && UnderAny(path, excludeDirs))
            {
                cntBoot++; // 사용자 제외는 부팅 보호와 동일 목적(사용자가 지정한 보호 경로)
                return false;
            }

            ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null)
            {
                return false;
            }

            // ② NormalMap → 단색 스텁이 degenerate normal, 런타임 LoadImage 가 sRGB 로 덮어써 라이팅 깨짐 → 제외.
            if (ti.textureType == TextureImporterType.NormalMap)
            {
                cntColorSpace++;
                return false;
            }

            // ② linear(sRGB=false) → LoadImage 는 항상 sRGB 로 기록, colorSpace 플래그는 객체 생성 시 고정 → 제외.
            if (!ti.sRGBTexture)
            {
                cntColorSpace++;
                return false;
            }

            return true;
        }

        // ─────────────────────────── 스텁 생성 ───────────────────────────

        /// <summary>동일 차원 단색(투명) RGBA32 스텁 PNG 를 <paramref name="dstFull"/> 에 쓴다. temp 텍스처는 즉시 파괴(leak 방지).</summary>
        private static bool WriteStubPng(string dstFull, int w, int h)
        {
            Texture2D tex = null;
            try
            {
                tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                var fill = new Color32[w * h]; // 기본값 = (0,0,0,0) 투명 — 균일 → crunch+brotli ~0.
                tex.SetPixels32(fill);
                tex.Apply(false, false);
                File.WriteAllBytes(dstFull, tex.EncodeToPNG());
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT-StreamingTexture] 스텁 생성 실패({dstFull}): {e.Message}");
                return false;
            }
            finally
            {
                if (tex != null)
                {
                    UnityEngine.Object.DestroyImmediate(tex);
                }
            }
        }

        // ─────────────────────────── 스트림 다운스케일 ───────────────────────────

        /// <summary>
        /// 균일 배율(cap÷최대변)로 다운스케일된 목표 차원을 산출한다. 두 축에 '동일 factor' 를 적용해
        /// 종횡비와 스프라이트시트 서브-rect UV 비율을 보존한다. 최대변이 cap 이하이거나 반올림 결과
        /// 축소가 없으면(둘 중 하나라도 원본 이상) false. 성공 시 nw/nh 는 1 이상 & 각각 원본 미만.
        /// </summary>
        internal static bool ComputeDownscaledDims(int w, int h, int cap, out int nw, out int nh)
        {
            nw = w;
            nh = h;
            if (w <= 0 || h <= 0 || cap < MinDownscaleSize)
            {
                return false;
            }

            int maxDim = Math.Max(w, h);
            if (maxDim <= cap)
            {
                return false; // 캡 이하 → 축소 없음.
            }

            double scale = (double)cap / maxDim;
            nw = Math.Max(1, (int)Math.Round(w * scale));
            nh = Math.Max(1, (int)Math.Round(h * scale));
            if (nw >= w || nh >= h)
            {
                return false; // 반올림 결과 축소 없음 → skip.
            }

            return true;
        }

        /// <summary>
        /// StreamingAssets 스트림 사본 이미지를 균일 배율(cap÷최대변)로 다운스케일해 제자리 덮어쓴다.
        /// GPU(Graphics.Blit/RenderTexture) 대신 CPU 영역(box) 평균 리샘플 — <c>-batchmode -nographics</c>
        /// 에서도 안전하고, 알파 프리멀티플라이 평균으로 투명 경계 dark-fringe 를 방지한다. temp 텍스처는
        /// 즉시 파괴(leak 방지). 인코딩은 확장자에 맞춤(.jpg/.jpeg → JPG q90, 그 외 → PNG).
        /// 성공 시 새 차원/바이트를 반환하고, 디코드/인코드 실패·무축소(반올림)면 원본을 그대로 두고 false.
        /// </summary>
        /// <summary>
        /// 이번 빌드에 적용할 다운스케일 캡을 해석한다(순수 판정 — 테스트 대상).
        /// 명시 활성(textureStreamDownscale==1)일 때만 사용자 캡을 존중하고, 자동(-1)은 SDK 안전 캡
        /// (GetDefaultTextureStreamDownscaleMaxSize)을 사용한다 — 구버전 AITConfig.asset 에 박제된
        /// 옛 기본값이 미래 posture/기본값 변경 후 의도 없이 적용되는 것을 차단한다
        /// (클램프 캡의 AITTextureSizeClampProcessor.ResolveClampMax 와 동일 규칙).
        /// </summary>
        internal static int ResolveDownscaleCap(AITEditorScriptObject config)
        {
            return config.textureStreamDownscale == 1
                ? config.textureStreamDownscaleMaxSize
                : AITDefaultSettings.GetDefaultTextureStreamDownscaleMaxSize();
        }

        private static bool TryDownscaleStreamImage(string absPath, int cap, out int newW, out int newH, out long beforeBytes, out long afterBytes)
        {
            newW = 0;
            newH = 0;
            beforeBytes = 0;
            afterBytes = 0;
            Texture2D src = null;
            Texture2D dst = null;
            try
            {
                if (!File.Exists(absPath))
                {
                    return false;
                }

                byte[] bytes = File.ReadAllBytes(absPath);
                beforeBytes = bytes.Length;

                src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!src.LoadImage(bytes, false)) // PNG/JPG 디코드 실패 → skip.
                {
                    return false;
                }

                int w = src.width;
                int h = src.height;
                if (!ComputeDownscaledDims(w, h, cap, out newW, out newH))
                {
                    return false; // 캡 이하이거나 반올림 결과 축소 없음 → skip.
                }

                Color32[] dstPixels = BoxDownsamplePremultiplied(src.GetPixels32(), w, h, newW, newH);
                dst = new Texture2D(newW, newH, TextureFormat.RGBA32, false);
                dst.SetPixels32(dstPixels);
                dst.Apply(false, false);

                string ext = Path.GetExtension(absPath).ToLowerInvariant();
                byte[] outBytes = (ext == ".jpg" || ext == ".jpeg") ? dst.EncodeToJPG(90) : dst.EncodeToPNG();
                if (outBytes == null || outBytes.Length == 0)
                {
                    return false;
                }

                File.WriteAllBytes(absPath, outBytes);
                afterBytes = outBytes.Length;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT-StreamingTexture]   다운스케일 실패({Path.GetFileName(absPath)}): {e.Message}");
                return false;
            }
            finally
            {
                if (src != null)
                {
                    UnityEngine.Object.DestroyImmediate(src);
                }

                if (dst != null)
                {
                    UnityEngine.Object.DestroyImmediate(dst);
                }
            }
        }

        /// <summary>
        /// Color32 픽셀 배열을 영역(box) 평균으로 다운샘플한다(src w×h → dst w×h, 축소 전용).
        /// RGB 는 알파 가중(프리멀티플라이) 평균 후 언프리멀티플라이 → 완전 투명 texel 의 색이 경계를
        /// 오염시키지 않는다(dark-fringe 방지). 알파는 단순 평균. 각 dst 픽셀의 소스 풋프린트를 모두 누적.
        /// </summary>
        internal static Color32[] BoxDownsamplePremultiplied(Color32[] src, int sw, int sh, int dw, int dh)
        {
            var dst = new Color32[dw * dh];
            float rx = (float)sw / dw;
            float ry = (float)sh / dh;
            for (int y = 0; y < dh; y++)
            {
                int sy0 = (int)(y * ry);
                int sy1 = Math.Max(sy0 + 1, (int)((y + 1) * ry));
                if (sy1 > sh)
                {
                    sy1 = sh;
                }

                for (int x = 0; x < dw; x++)
                {
                    int sx0 = (int)(x * rx);
                    int sx1 = Math.Max(sx0 + 1, (int)((x + 1) * rx));
                    if (sx1 > sw)
                    {
                        sx1 = sw;
                    }

                    long aSum = 0, rSum = 0, gSum = 0, bSum = 0;
                    int cnt = 0;
                    for (int yy = sy0; yy < sy1; yy++)
                    {
                        int row = yy * sw;
                        for (int xx = sx0; xx < sx1; xx++)
                        {
                            Color32 c = src[row + xx];
                            int a = c.a;
                            aSum += a;
                            rSum += (long)c.r * a; // 알파 가중(프리멀티플라이).
                            gSum += (long)c.g * a;
                            bSum += (long)c.b * a;
                            cnt++;
                        }
                    }

                    if (cnt == 0)
                    {
                        cnt = 1;
                    }

                    byte outA = (byte)(aSum / cnt);
                    byte outR, outG, outB;
                    if (aSum > 0)
                    {
                        outR = (byte)(rSum / aSum); // 언프리멀티플라이.
                        outG = (byte)(gSum / aSum);
                        outB = (byte)(bSum / aSum);
                    }
                    else
                    {
                        outR = 0; // 완전 투명 → 색 무의미.
                        outG = 0;
                        outB = 0;
                    }

                    dst[y * dw + x] = new Color32(outR, outG, outB, outA);
                }
            }

            return dst;
        }

        // ─────────────────────────── 백업/복원 ───────────────────────────

        /// <summary>단일 텍스처의 부분 변경(스텁/백업)을 즉시 되돌린다(스텁 생성 실패 경로).</summary>
        private static void RevertSingle(string srcFull, string metaFull, string srcBak, string metaBak, string projectRoot)
        {
            try
            {
                if (File.Exists(srcBak))
                {
                    File.Copy(srcBak, srcFull, true);
                    File.Delete(srcBak);
                }

                if (File.Exists(metaBak))
                {
                    File.Copy(metaBak, metaFull, true);
                    File.Delete(metaBak);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT-StreamingTexture] 부분 복원 실패({srcFull}): {e.Message}");
            }
        }

        /// <summary>Assets 트리의 모든 *.aittexstreambak / *.aittexstreammetabak 를 원본으로 되돌리고 백업을 삭제한다. 복원 개수 반환.</summary>
        private static int RestoreAllBackups()
        {
            int restored = 0;
            string assetsPath = Application.dataPath;
            string projectRoot = Directory.GetParent(assetsPath).FullName;

            // .meta 백업 먼저 복원(임포터 설정), 그다음 소스 복원 — 순서 무관하나 한 번에 reimport 하도록 경로 수집.
            var reimport = new HashSet<string>();

            restored += RestoreBySuffix(assetsPath, projectRoot, MetaBackupSuffix, true, reimport);
            restored += RestoreBySuffix(assetsPath, projectRoot, SrcBackupSuffix, false, reimport);

            foreach (var rel in reimport)
            {
                try
                {
                    AssetDatabase.ImportAsset(rel, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[AIT-StreamingTexture] reimport 실패({rel}): {e.Message}");
                }
            }

            return restored;
        }

        /// <summary>주어진 접미사 백업을 복원하고, 복원 대상 에셋의 프로젝트 상대 경로를 <paramref name="reimport"/> 에 모은다.</summary>
        private static int RestoreBySuffix(string assetsPath, string projectRoot, string suffix, bool isMeta, HashSet<string> reimport)
        {
            int restored = 0;
            string[] backups;
            try
            {
                backups = Directory.GetFiles(assetsPath, "*" + suffix, SearchOption.AllDirectories);
            }
            catch
            {
                return 0;
            }

            foreach (var bak in backups)
            {
                string original = bak.Substring(0, bak.Length - suffix.Length); // meta: "<src>.meta", src: "<src>"
                try
                {
                    File.Copy(bak, original, true);
                    File.Delete(bak);

                    string assetFull = isMeta && original.EndsWith(".meta")
                        ? original.Substring(0, original.Length - ".meta".Length)
                        : original;
                    string rel = AbsoluteToProjectRelative(assetFull, projectRoot);
                    if (!string.IsNullOrEmpty(rel))
                    {
                        reimport.Add(rel);
                    }

                    restored++;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[AIT-StreamingTexture] 백업 복원 실패({bak}): {e.Message}");
                }
            }

            return restored;
        }

        /// <summary>에디터 로드 시 안전망. 마커가 잔존하면(=이전 빌드가 복원 전에 종료) 백업을 자동 복원.</summary>
        private static void SafetyNetRestore()
        {
            try
            {
                string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                string markerFull = Path.Combine(projectRoot, MarkerRelative);
                if (!File.Exists(markerFull))
                {
                    return; // 공통 경로: 잔존물 없음(빠른 반환)
                }

                int restored = RestoreAllBackups();
                RemoveStreamRoot();
                RemoveMarker();
                if (restored > 0)
                {
                    AssetDatabase.Refresh();
                    Debug.LogWarning($"[AIT-StreamingTexture] 안전망: 이전 빌드 잔존 백업 {restored}개를 복원했습니다.");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT-StreamingTexture] 안전망 복원 중 예외(무시): {e}");
            }
        }

        private static void RemoveStreamRoot()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string streamRootFull = Path.Combine(projectRoot, StreamRootAssets);
            try
            {
                if (Directory.Exists(streamRootFull))
                {
                    Directory.Delete(streamRootFull, true);
                }

                string meta = streamRootFull + ".meta";
                if (File.Exists(meta))
                {
                    File.Delete(meta);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT-StreamingTexture] StreamingAssets 사본 제거 실패: {e.Message}");
            }
        }

        private static void CreateMarker()
        {
            try
            {
                string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                File.WriteAllText(Path.Combine(projectRoot, MarkerRelative), "active");
            }
            catch
            {
                // 마커 생성 실패는 치명적이지 않음(안전망 가속용일 뿐).
            }
        }

        private static void RemoveMarker()
        {
            try
            {
                string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                string m = Path.Combine(projectRoot, MarkerRelative);
                if (File.Exists(m))
                {
                    File.Delete(m);
                }
            }
            catch
            {
                // 무시
            }
        }

        private static string AbsoluteToProjectRelative(string absolute, string projectRoot)
        {
            string norm = absolute.Replace('\\', '/');
            string root = projectRoot.Replace('\\', '/').TrimEnd('/') + "/";
            return norm.StartsWith(root) ? norm.Substring(root.Length) : null;
        }

        private static string[] SplitDirs(string v)
            => string.IsNullOrEmpty(v) ? null : v.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

        private static bool UnderAny(string path, string[] dirs)
        {
            foreach (var d in dirs)
            {
                var t = d.Trim().TrimEnd('/');
                if (path.StartsWith(t + "/") || path == t)
                {
                    return true;
                }
            }

            return false;
        }

        private static string JsonStr(string s)
            => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

        // 존재하지 않는 경로에 대해 조용히 no-op(brotli 채택/폐기 정리용).
        private static void SafeDelete(string full)
        {
            try
            {
                if (File.Exists(full))
                {
                    File.Delete(full);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT-StreamingTexture] 파일 삭제 실패(무시): {full} — {e.Message}");
            }
        }
    }
}
