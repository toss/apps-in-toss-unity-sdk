// -----------------------------------------------------------------------
// <copyright file="AITFontExternalizer.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Font Deferral (build-time externalizer)
// </copyright>
// -----------------------------------------------------------------------
//
// 빌드 직전, 비-부팅 대형 폰트(예: 게임이 임베드한 NotoSansSC/TC/JP 등 비-한국어 CJK)를
// 초기 .data 밖(StreamingAssets)의 WebGL AssetBundle 로 빼내고, 프로젝트 내 소스 폰트는
// 빌드 동안 최소 유효 스텁 .ttf(612B)로 치환해 .data 에 풀 .ttf 바이트가 들어가지 않게 한다. 런타임
// (AppsInToss.AITStreamingFont)이 interactive(=첫 프레임) 이후 비동기로 번들을 로드하여
// 그 안의 TMP_FontAsset 을 TMP fallback 체인에 주입한다 → 부팅엔 빠지고 이후 CJK 재수화.
//
// 메커니즘(왜 텍스처/오디오 in-place 가 안 되고 fallback 주입인가):
//   텍스처는 Texture2D.LoadImage 로, 오디오는 AudioSource.clip 재할당으로 살아있는 객체에
//   제자리 복원이 가능하다. 그러나 폰트는 런타임에 .ttf 바이트→Font 생성 경로가 WebGL 에서
//   구조적으로 막혀 있어(엔진이 임의 바이트 폰트 로딩을 노출하지 않음) "원본 Font 를 제자리
//   복원"하는 것이 불가능하다. 대신 풀 폰트를 담은 AssetBundle 을 런타임에 로드하여 그 안의
//   TMP_FontAsset 을 TMP 글로벌 fallback 목록에 "추가"한다 — 부팅 빌드의 소스-비운 폰트는
//   primary 로 남아 CJK □ 를 그리지만, fallback 의 풀 폰트가 누락 글리프를 채운다.
//
// 부팅 안전: 부팅 씬(level0)이 CJK 0 글자인 게임(R4 인벤토리)에서 소스-비운 비-한국어 폰트는
//   부팅 화면에 □ 조차 보이지 않는다. 언어선택 등 비-부팅 화면에서만 일시 □ → 재수화로 해소.
//   스텁(또는 빈) 소스의 Dynamic TMP 는 Unity 에서 크래시 없이 □ 폴백한다
//   (SPIKE_OPTIMIZATION.md §3-U 실측으로 boot 10/10 확인).
//
// 왜 스텁 치환인가(중요): WebGL 은 시스템 폰트가 없어 엔진이 includeFontData=false 를 ★강제 무시★하고
//   풀 .ttf 를 무조건 .data 에 굽는다("Including Font data even though the importer is set not to.").
//   따라서 importer 토글로는 .data 가 줄지 않는다(실측 확인). 소스 .ttf 자체를 작게 만드는 것이 유일하게
//   동작하는 빌드타임 메커니즘이다.
//
// 비파괴: 소스 폰트의 원본 .ttf 바이트를 <src>.aitfontsrcbak~ 로 인플레이스 백업하고(트레일링 '~' 라
//   Unity 임포트에서 제외 + Assets 내 보관이라 VCS/Library 복구 가능), 빌드 종료(성공/실패 무관) 시 원본
//   복원 + reimport 로 원상 복귀.
//   비정상 종료 시 다음 에디터 로드의 안전망(SafetyNetRestore, 마커 게이트)이 자동 복원한다.
//
// 화이트리스트 전용: 동적 텍스트 □ 리스크 때문에 fontStreamingTargetPaths(TMP_FontAsset 경로)
//   가 명시된 경우에만 동작한다(프로젝트 전체 폰트 자동 스캔 금지).
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
    /// 빌드 단계 대형 폰트 외부화/복원 처리기. <see cref="AITEditorScriptObject.enableFontStreaming"/>
    /// 설정에 따라 동작하며, 런타임 컴포넌트 <c>AppsInToss.AITStreamingFont</c> 와 짝을 이룬다.
    /// </summary>
    [InitializeOnLoad]
    public static class AITFontExternalizer
    {
        /// <summary>외부화된 번들/매니페스트가 놓이는 프로젝트 상대 경로.</summary>
        private const string StreamRootAssets = "Assets/StreamingAssets/ait-stream-font";

        /// <summary>치환 전 원본 .ttf/.otf 바이트를 보관하는 인플레이스 백업 접미사. 트레일링 '~' 라서
        /// Unity 임포트에서 제외되고, Assets 트리 내 보관이라 Library 삭제·VCS 체크아웃으로도 복구 가능
        /// (안전망/복원의 기준 파일).</summary>
        private const string SrcBackupSuffix = ".aitfontsrcbak~";

        /// <summary>
        /// 빌드 동안 대상 소스 .ttf 를 치환하는 최소 유효 TTF 스텁(base64, 612B, .notdef+space만).
        /// WebGL 은 시스템 폰트가 없어 <c>includeFontData=false</c> 를 ★엔진이 강제 무시★하고 풀 폰트를
        /// 무조건 .data 에 굽는다("Including Font data even though the importer is set not to."). 따라서
        /// .data 에서 폰트 바이트를 빼는 유일한 빌드타임 메커니즘은 소스 .ttf 자체를 작게 만드는 것이다.
        /// 누락된 CJK 글리프는 런타임 글로벌 fallback 재수화(AITStreamingFont)가 풀 폰트 번들로 채운다
        /// (빈/스텁 소스의 Dynamic TMP 는 크래시 없이 □ 폴백 — §3-U boot 10/10 실측).
        /// </summary>
        private const string StubTtfBase64 =
            "AAEAAAAKAIAAAwAgT1MvMkkYTDAAAAEoAAAAYGNtYXAADABzAAABkAAAADRnbHlmAAAAAAAAAcwAAAABaGVhZCupKBAAAACsAAAANmhoZWEGaAZoAAAA5AAAACRobXR4CAAAAAAAAYgAAAAGbG9jYQAAAAAAAAHEAAAABm1heHAAAwACAAABCAAAACBuYW1lpGT3tQAAAdAAAABscG9zdAAHAAAAAAI8AAAAJgABAAAAAQAAVk+3Zl8PPPUAAwgAAAAAAOZJcYwAAAAA5klxjAAAAAAAAAAAAAAAAwACAAAAAAAAAAEAAAZm/mYAAAgAAAAAAAAAAAEAAAAAAAAAAAAAAAAAAAABAAEAAAACAAAAAAAAAAAAAgAAAAAAAAAAAAAAAAAAAAAAAwgAAZAABQAEAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABAAAAAAAAAAAAAAAAPz8/PwAAACAAIAZm/mYAAAZmAZoAAAAAAAAAAAAAAAAAAAAgAAAIAAAAAAAAAAAAAAIAAAADAAAAFAADAAEAAAAUAAQAIAAAAAQABAABAAAAIP//AAAAIP///+EAAQAAAAAAAAAAAAAAAAAAAAAAAAAEADYAAQAAAAAAAQALAAAAAQAAAAAAAgAHAAsAAwABBAkAAQAWABIAAwABBAkAAgAOAChBSVRGb250U3R1YlJlZ3VsYXIAQQBJAFQARgBvAG4AdABTAHQAdQBiAFIAZQBnAHUAbABhAHIAAgAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAACAAAAAwAA";

        /// <summary>apply 진행 중임을 표시하는 마커(Unity 가 무시하는 '.' 접두 숨김 파일).</summary>
        private const string MarkerRelative = "Assets/.ait-fontstream-active";

        /// <summary>AssetBundle 빌드 임시 출력 디렉토리(프로젝트 상대, Library 하위 = VCS 무시).</summary>
        private const string BundleTempDir = "Library/ait-fontbundle";

        /// <summary>한 번의 외부화 결과 핸들. finally 에서 정확한 복원에 사용.</summary>
        public sealed class FontStreamHandle
        {
            /// <summary>이번 빌드에서 외부화가 실제로 수행되었는지.</summary>
            public bool Active;

            /// <summary>외부화된 폰트 개수.</summary>
            public int Count;
        }

        /// <summary>외부화 계획 1건(번들 빌드와 소스 스텁 치환을 분리 실행하기 위한 중간 표현).</summary>
        private sealed class PlanEntry
        {
            public string Guid;
            public string TmpAssetPath;
            public string SrcFontPath;
            public string BundleFileName;

            /// <summary>스텁 치환 전 원본 소스 폰트 크기(바이트). Phase A 에서 캡처해 두지 않으면
            /// Phase D 시점엔 이미 612B 스텁으로 치환돼 있어 0MB 로 잘못 보고된다.</summary>
            public long SrcBytes;
        }

        static AITFontExternalizer()
        {
            // 에디터 로드 시 안전망: 이전 빌드가 비정상 종료되어 복원이 누락된 경우(마커 잔존) 자동 복원.
            EditorApplication.delayCall += SafetyNetRestore;
        }

        /// <summary>
        /// 빌드 직전 호출: 설정이 켜져 있고 대상이 지정돼 있으면 대상 폰트를 AssetBundle 로 외부화하고
        /// 소스 폰트를 최소 스텁 .ttf 로 치환해 .data 에서 제외한다.
        /// </summary>
        /// <param name="config">프로젝트 에디터 설정. null·비활성·화이트리스트 미지정 시 no-op.</param>
        /// <returns>복원에 사용할 핸들(항상 non-null).</returns>
        public static FontStreamHandle ExternalizeForBuild(AITEditorScriptObject config)
        {
            var handle = new FontStreamHandle();
            if (config == null || !config.enableFontStreaming)
            {
                return handle;
            }

            string[] targets = SplitList(config.fontStreamingTargetPaths);
            if (targets == null || targets.Length == 0)
            {
                Debug.Log("[AIT-StreamingFont] 대상 TMP_FontAsset 경로가 비어 있어 건너뜁니다(fontStreamingTargetPaths). 동적 텍스트 안전을 위해 명시적 지정이 필요합니다.");
                return handle;
            }

            try
            {
                string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                string streamRootFull = Path.Combine(projectRoot, StreamRootAssets);
                string bundleTempFull = Path.Combine(projectRoot, BundleTempDir);

                // 대상 화이트리스트 정규화(소스 공유 역참조 검사에 사용).
                var targetSet = new HashSet<string>();
                foreach (var t in targets)
                {
                    string norm = (t ?? string.Empty).Trim().Replace('\\', '/');
                    if (norm.Length > 0)
                    {
                        targetSet.Add(norm);
                    }
                }

                CreateMarker();
                Directory.CreateDirectory(streamRootFull);
                Directory.CreateDirectory(bundleTempFull);

                // ── Phase A: 검증 + 역참조 안전검사 → 외부화 계획 수립(아직 소스 스텁 치환 안 함) ──
                var plan = new List<PlanEntry>();
                foreach (var rawTarget in targets)
                {
                    string tmpAssetPath = rawTarget.Trim().Replace('\\', '/');
                    if (string.IsNullOrEmpty(tmpAssetPath) || !tmpAssetPath.StartsWith("Assets/"))
                    {
                        Debug.LogWarning($"[AIT-StreamingFont]   대상 제외(Assets/ 밖): {tmpAssetPath}");
                        continue;
                    }

                    string tmpFull = Path.Combine(projectRoot, tmpAssetPath);
                    if (!File.Exists(tmpFull))
                    {
                        Debug.LogWarning($"[AIT-StreamingFont]   대상 없음: {tmpAssetPath}");
                        continue;
                    }

                    string guid = AssetDatabase.AssetPathToGUID(tmpAssetPath);
                    if (string.IsNullOrEmpty(guid))
                    {
                        Debug.LogWarning($"[AIT-StreamingFont]   GUID 산출 실패: {tmpAssetPath}");
                        continue;
                    }

                    // 소스 .ttf/.otf(스텁 치환 대상)를 TMP_FontAsset 의존성에서 해석.
                    string srcFontPath = ResolveSourceFont(tmpAssetPath);
                    if (string.IsNullOrEmpty(srcFontPath))
                    {
                        Debug.LogWarning($"[AIT-StreamingFont]   소스 .ttf/.otf 해석 실패(의존성에 폰트 없음) → 건너뜀: {tmpAssetPath}");
                        continue;
                    }

                    // 안전: 같은 소스 .ttf 를 화이트리스트 "밖" TMP_FontAsset 도 쓰면, 스텁 치환이
                    //       번들 없는 그 폰트의 글리프까지 비워 시각 파손(부팅 포함) → 보수적으로 외부화 스킵.
                    if (IsSourceSharedByNonTarget(srcFontPath, targetSet))
                    {
                        Debug.LogWarning($"[AIT-StreamingFont]   소스 공유 감지({Path.GetFileName(srcFontPath)}) — 화이트리스트 밖 폰트가 같은 .ttf 사용 → 외부화 스킵(시각 파손 방지): {tmpAssetPath}");
                        continue;
                    }

                    plan.Add(new PlanEntry
                    {
                        Guid = guid,
                        TmpAssetPath = tmpAssetPath,
                        SrcFontPath = srcFontPath,
                        BundleFileName = guid + ".bundle",
                        // 스텁 치환(Phase C) 전 원본 크기를 지금 캡처 — defer 리포트의 정확도를 위해.
                        SrcBytes = SafeFileSize(Path.Combine(projectRoot, srcFontPath)),
                    });
                }

                // ── Phase B: 모든 번들을 먼저 빌드(소스가 전부 아직 원본 → 풀 폰트 캡처). ──
                //    이렇게 해야 같은 소스를 공유하는 다중 대상에서 '이미 스텁된 소스'를 캡처하는 순서 위험이 없다.
                var built = new List<PlanEntry>();
                foreach (var p in plan)
                {
                    if (BuildFontBundle(p.TmpAssetPath, p.BundleFileName, bundleTempFull, streamRootFull))
                    {
                        built.Add(p);
                    }
                    else
                    {
                        Debug.LogWarning($"[AIT-StreamingFont]   번들 빌드 실패 → 건너뜀: {p.TmpAssetPath}");
                    }
                }

                // ── Phase C: 소스별 1회 스텁 치환(성공한 번들 대상의 소스만). ──
                var disabledSources = new HashSet<string>();
                var failedSources = new HashSet<string>();
                foreach (var p in built)
                {
                    if (disabledSources.Contains(p.SrcFontPath) || failedSources.Contains(p.SrcFontPath))
                    {
                        continue;
                    }

                    if (SwapSourceToStub(p.SrcFontPath, projectRoot))
                    {
                        disabledSources.Add(p.SrcFontPath);
                    }
                    else
                    {
                        failedSources.Add(p.SrcFontPath);
                        Debug.LogWarning($"[AIT-StreamingFont]   소스 스텁 치환 실패 → 해당 소스 엔트리 드롭: {p.SrcFontPath}");
                    }
                }

                // ── Phase D: 스텁 치환 성공 소스만 매니페스트에 채택, 실패 소스 엔트리는 번들 제거(다운로드 낭비 방지). ──
                var entries = new List<string>();
                int n = 0;
                long deferredBytes = 0;
                foreach (var p in built)
                {
                    if (!disabledSources.Contains(p.SrcFontPath))
                    {
                        SafeDelete(Path.Combine(streamRootFull, p.BundleFileName));
                        continue;
                    }

                    string[] fontNames = CollectFontAssetNames(p.TmpAssetPath);
                    entries.Add("{\"guid\":\"" + p.Guid + "\",\"bundle\":" + JsonStr(p.BundleFileName)
                                + ",\"fonts\":[" + string.Join(",", Array.ConvertAll(fontNames, JsonStr)) + "]}");
                    n++;
                    // Phase A 에서 스텁 치환 전 캡처한 원본 크기(p.SrcBytes)를 보고. 지금 파일을 다시
                    // 재면 이미 612B 스텁이라 0.00MB 로 잘못 나온다.
                    long srcBytes = p.SrcBytes;
                    deferredBytes += srcBytes;
                    Debug.Log($"[AIT-StreamingFont]   외부화 {Path.GetFileName(p.TmpAssetPath)} (소스 {Path.GetFileName(p.SrcFontPath)} {srcBytes / 1048576f:0.00}MB) → {p.BundleFileName}");
                }

                handle.Active = n > 0;
                handle.Count = n;
                if (handle.Active)
                {
                    // 매니페스트 동봉(런타임 AITStreamingFont 가 읽는 계약: maxConcurrent + entries). n>0 일 때만 기록.
                    int maxConcurrent = config.fontStreamingMaxConcurrent > 0 ? config.fontStreamingMaxConcurrent : 2;
                    var sb = new StringBuilder();
                    sb.Append("{\"maxConcurrent\":").Append(maxConcurrent)
                      .Append(",\"entries\":[").Append(string.Join(",", entries)).Append("]}");
                    File.WriteAllText(Path.Combine(streamRootFull, "manifest.json"), sb.ToString());
                    AssetDatabase.Refresh();
                    Debug.Log($"[AIT-StreamingFont] ✓ {n}개 폰트 외부화, 소스 {deferredBytes / 1048576f:0.0}MB defer(초기 .data 제거).");
                }
                else
                {
                    // 외부화 0건 → 루프 중 생긴 잔존물(백업/번들/마커)을 모두 정리(비파괴 보증).
                    RestoreAllBackups();
                    RemoveStreamRoot();
                    RemoveBundleTemp();
                    RemoveMarker();
                    AssetDatabase.Refresh();
                    Debug.Log("[AIT-StreamingFont] 외부화 대상 0개 → 원상 복귀.");
                }

                return handle;
            }
            catch (Exception e)
            {
                // 외부화 실패가 빌드 전체를 막지 않도록: 부분 변경을 즉시 복원하고 비활성 핸들 반환.
                Debug.LogError($"[AIT-StreamingFont] 외부화 예외 → 복원 후 건너뜀: {e}");
                RestoreAllBackups();
                RemoveStreamRoot();
                RemoveBundleTemp();
                RemoveMarker();
                AssetDatabase.Refresh();
                return new FontStreamHandle();
            }
        }

        /// <summary>
        /// 빌드 종료 후(성공/실패 무관) 호출: 스텁 치환된 소스 .ttf 를 원본 바이트 복원으로 원상 복귀하고 StreamingAssets 번들을 제거한다.
        /// </summary>
        public static void RestoreForBuild(FontStreamHandle handle)
        {
            if (handle == null || !handle.Active)
            {
                return;
            }

            try
            {
                int restored = RestoreAllBackups();
                RemoveStreamRoot();
                RemoveBundleTemp();
                RemoveMarker();
                AssetDatabase.Refresh();
                Debug.Log($"[AIT-StreamingFont] 복원 완료: {restored}개 폰트 임포터 원상, StreamingAssets 번들 제거");
            }
            catch (Exception e)
            {
                Debug.LogError($"[AIT-StreamingFont] 복원 예외: {e}");
            }
        }

        // ─────────────────────────── 외부화 단계 ───────────────────────────

        /// <summary>TMP_FontAsset 의 의존성 중 첫 .ttf/.otf 소스 Font 경로를 반환(없으면 null).</summary>
        private static string ResolveSourceFont(string tmpAssetPath)
        {
            try
            {
                foreach (var dep in AssetDatabase.GetDependencies(tmpAssetPath, true))
                {
                    if (string.IsNullOrEmpty(dep) || dep == tmpAssetPath)
                    {
                        continue;
                    }

                    string ext = Path.GetExtension(dep).ToLowerInvariant();
                    if (ext == ".ttf" || ext == ".otf")
                    {
                        return dep;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT-StreamingFont] 소스 폰트 해석 경고({tmpAssetPath}): {e.Message}");
            }

            return null;
        }

        /// <summary>
        /// 소스 .ttf/.otf 를 화이트리스트 "밖" TMP_FontAsset 이 ★자체 소스(직접 의존)★로도 쓰는지 검사.
        /// true 면 외부화 금지(번들 없는 그 폰트의 글리프까지 비워 영구 시각 파손). fallback 체인을 통한
        /// 전이 참조는 런타임 글로벌 fallback 재수화가 커버하므로 공유로 보지 않는다(직접 의존만 판정).
        /// </summary>
        private static bool IsSourceSharedByNonTarget(string srcFontPath, HashSet<string> targetSet)
        {
            try
            {
                string srcNorm = srcFontPath.Replace('\\', '/');
                foreach (var fontGuid in AssetDatabase.FindAssets("t:TMP_FontAsset"))
                {
                    string p = AssetDatabase.GUIDToAssetPath(fontGuid);
                    if (string.IsNullOrEmpty(p))
                    {
                        continue;
                    }

                    p = p.Replace('\\', '/');
                    if (targetSet.Contains(p))
                    {
                        continue; // 화이트리스트 대상은 함께 외부화되므로 공유로 보지 않음.
                    }

                    // 직접 의존만 검사(false): 소스 .ttf 가 ★그 폰트의 자체 소스★일 때만 공유로 판정한다.
                    // 재귀(true)면 KR 주폰트(Main SDF)의 fallback 체인이 CJK .ttf 를 전이 참조해 false-positive →
                    // 전체 기능 무력화(실측 확인). fallback 경유 글리프는 런타임 글로벌 fallback 재수화가 커버하므로 안전.
                    foreach (var dep in AssetDatabase.GetDependencies(p, false))
                    {
                        if (!string.IsNullOrEmpty(dep)
                            && string.Equals(dep.Replace('\\', '/'), srcNorm, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                // 검사 자체가 실패하면 '공유'로 단정해 기능을 죽이기보다, 진행(false)하되 경고로 가시화한다.
                Debug.LogWarning($"[AIT-StreamingFont] 소스 공유 검사 경고({srcFontPath}): {e.Message}");
                return false;
            }

            return false;
        }

        /// <summary>지정 TMP_FontAsset 을 단일 WebGL AssetBundle 로 빌드하여 StreamingAssets 로 복사. 성공 시 true.</summary>
        private static bool BuildFontBundle(string tmpAssetPath, string bundleFileName, string bundleTempFull, string streamRootFull)
        {
            try
            {
                var build = new AssetBundleBuild
                {
                    assetBundleName = bundleFileName,
                    assetNames = new[] { tmpAssetPath },
                };

                // ChunkBasedCompression(LZ4): 런타임 LoadFromMemoryAsync 가 빠름(post-boot 배경 로드라 TTFF 무관).
                // DeterministicAssetBundle: 동일 입력 → 동일 출력(재현성). BuildTarget.WebGL: 플레이어와 동일 타깃.
                var manifest = BuildPipeline.BuildAssetBundles(
                    bundleTempFull,
                    new[] { build },
                    BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.DeterministicAssetBundle,
                    BuildTarget.WebGL);

                if (manifest == null)
                {
                    return false;
                }

                string built = Path.Combine(bundleTempFull, bundleFileName);
                if (!File.Exists(built))
                {
                    Debug.LogWarning($"[AIT-StreamingFont] 번들 산출물 없음: {built}");
                    return false;
                }

                File.Copy(built, Path.Combine(streamRootFull, bundleFileName), true);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT-StreamingFont] 번들 빌드 예외({tmpAssetPath}): {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 소스 .ttf/.otf 의 원본 바이트를 인플레이스 백업하고, 최소 유효 TTF 스텁으로 파일 내용을 치환한 뒤
        /// reimport 하여 빌드 .data 에서 폰트 바이트를 제거한다(GUID/.meta 불변 → TMP_FontAsset 참조 유지).
        /// 성공 시 true. WebGL 에서 includeFontData=false 가 강제 무시되는 문제를 우회하는 유일한 빌드타임 경로.
        /// </summary>
        private static bool SwapSourceToStub(string srcFontPath, string projectRoot)
        {
            string createdBak = null; // 이 호출이 새로 만든 백업만 실패 시 정리 대상(기존 백업 보호).
            try
            {
                var importer = AssetImporter.GetAtPath(srcFontPath) as TrueTypeFontImporter;
                if (importer == null)
                {
                    Debug.LogWarning($"[AIT-StreamingFont] TrueTypeFontImporter 아님(skip): {srcFontPath}");
                    return false;
                }

                string srcFull = Path.Combine(projectRoot, srcFontPath);
                string bak = srcFull + SrcBackupSuffix;

                // 이미 백업이 있으면(=이미 스텁으로 치환됨) 멱등 반환 — 원본 백업을 덮어쓰지 않는다.
                if (File.Exists(bak))
                {
                    return true;
                }

                if (!File.Exists(srcFull))
                {
                    Debug.LogWarning($"[AIT-StreamingFont] 소스 파일 없음(skip): {srcFull}");
                    return false;
                }

                // 원본 바이트 인플레이스 백업 → 스텁 바이트로 치환 → reimport(작은 Font 아티팩트 생성).
                File.Copy(srcFull, bak, true);
                createdBak = bak;
                File.WriteAllBytes(srcFull, Convert.FromBase64String(StubTtfBase64));
                AssetDatabase.ImportAsset(srcFontPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                return true;
            }
            catch (Exception e)
            {
                // 치환 실패: 방금 만든 백업이 있으면 원본을 즉시 되돌리고 백업 제거(비파괴 보증).
                if (!string.IsNullOrEmpty(createdBak) && File.Exists(createdBak))
                {
                    try
                    {
                        string srcFull = createdBak.Substring(0, createdBak.Length - SrcBackupSuffix.Length);
                        File.Copy(createdBak, srcFull, true);
                        SafeDelete(createdBak);
                        AssetDatabase.ImportAsset(srcFontPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                    }
                    catch
                    {
                        // 무시 — 안전망(SafetyNetRestore)이 다음 에디터 로드에서 재시도.
                    }
                }

                Debug.LogWarning($"[AIT-StreamingFont] 소스 스텁 치환 예외({srcFontPath}): {e.Message}");
                return false;
            }
        }

        /// <summary>TMP_FontAsset(.asset) 안에 든 폰트 에셋의 이름을 수집(매니페스트 진단용). 실패 시 파일명 기반 폴백.</summary>
        private static string[] CollectFontAssetNames(string tmpAssetPath)
        {
            var names = new List<string>();
            try
            {
                foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(tmpAssetPath))
                {
                    if (obj == null)
                    {
                        continue;
                    }

                    // TMP 컴파일 의존 없이 타입명으로 식별.
                    if (obj.GetType().Name == "TMP_FontAsset" && !string.IsNullOrEmpty(obj.name))
                    {
                        names.Add(obj.name);
                    }
                }
            }
            catch
            {
                // 무시 — 폴백.
            }

            if (names.Count == 0)
            {
                names.Add(Path.GetFileNameWithoutExtension(tmpAssetPath));
            }

            return names.ToArray();
        }

        // ─────────────────────────── 백업/복원 ───────────────────────────

        /// <summary>Assets 트리의 모든 *.aitfontsrcbak~ 를 원본 .ttf/.otf 로 되돌리고 백업을 삭제 + reimport. 복원 개수 반환.</summary>
        private static int RestoreAllBackups()
        {
            int restored = 0;
            string assetsPath = Application.dataPath;
            string projectRoot = Directory.GetParent(assetsPath).FullName;

            string[] backups;
            try
            {
                backups = Directory.GetFiles(assetsPath, "*" + SrcBackupSuffix, SearchOption.AllDirectories);
            }
            catch
            {
                return 0;
            }

            var reimport = new HashSet<string>();
            foreach (var bak in backups)
            {
                string srcOriginal = bak.Substring(0, bak.Length - SrcBackupSuffix.Length); // "<src>.ttf"
                try
                {
                    File.Copy(bak, srcOriginal, true);
                    File.Delete(bak);

                    string rel = AbsoluteToProjectRelative(srcOriginal, projectRoot);
                    if (!string.IsNullOrEmpty(rel))
                    {
                        reimport.Add(rel);
                    }

                    restored++;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[AIT-StreamingFont] 백업 복원 실패({bak}): {e.Message}");
                }
            }

            foreach (var rel in reimport)
            {
                try
                {
                    AssetDatabase.ImportAsset(rel, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[AIT-StreamingFont] reimport 실패({rel}): {e.Message}");
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
                if (!File.Exists(Path.Combine(projectRoot, MarkerRelative)))
                {
                    return; // 공통 경로: 잔존물 없음(빠른 반환)
                }

                int restored = RestoreAllBackups();
                RemoveStreamRoot();
                RemoveBundleTemp();
                RemoveMarker();
                if (restored > 0)
                {
                    AssetDatabase.Refresh();
                    Debug.LogWarning($"[AIT-StreamingFont] 안전망: 이전 빌드 잔존 폰트 백업 {restored}개를 복원했습니다.");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT-StreamingFont] 안전망 복원 중 예외(무시): {e}");
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
                Debug.LogWarning($"[AIT-StreamingFont] StreamingAssets 번들 제거 실패: {e.Message}");
            }
        }

        /// <summary>AssetBundle 빌드 임시 디렉토리(Library/ait-fontbundle)를 제거(빌드 반복 시 사이드카 누적 방지).</summary>
        private static void RemoveBundleTemp()
        {
            try
            {
                string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                string bundleTempFull = Path.Combine(projectRoot, BundleTempDir);
                if (Directory.Exists(bundleTempFull))
                {
                    Directory.Delete(bundleTempFull, true);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT-StreamingFont] 번들 임시 디렉토리 제거 실패: {e.Message}");
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

        // ─────────────────────────── 유틸 ───────────────────────────

        private static long SafeFileSize(string full)
        {
            try
            {
                return File.Exists(full) ? new FileInfo(full).Length : 0;
            }
            catch
            {
                return 0;
            }
        }

        private static void SafeDelete(string full)
        {
            try
            {
                if (File.Exists(full))
                {
                    File.Delete(full);
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

        private static string[] SplitList(string v)
            => string.IsNullOrEmpty(v) ? null : v.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

        private static string JsonStr(string s)
            => "\"" + (s ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }
}
