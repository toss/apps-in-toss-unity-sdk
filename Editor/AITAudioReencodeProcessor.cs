// -----------------------------------------------------------------------
// <copyright file="AITAudioReencodeProcessor.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Audio Re-encode (build-time AudioImporter override)
// </copyright>
// -----------------------------------------------------------------------
//
// 빌드 직전, 대상 AudioClip 의 WebGL 임포터 설정 중 compressionFormat/quality "만" 일시적으로
//   override(Vorbis + quality) 하여 reimport 함으로써, Unity 가 더 작은 오디오를 .data(및 CDN)에
//   굽게 한다. 빌드 종료(성공/실패 무관) 후 원본 임포트 설정으로 원상 복원한다.
//
// posture(기본 ON, opt-out):
//   SDK 는 이미 lossy 텍스처 최적화(crunch=DXT 압축, ASTC 블록 에스컬레이션)를 기본 ON 으로 둔다.
//   미니앱 플랫폼(200MB 캡·모바일 다운로드 민감)에서 그게 SDK 의 존재 이유이므로 오디오도 동일 posture.
//   ⚠ 단 "안전한 자동"을 위해 자동 모드는 비압축(PCM)/ADPCM 만 Vorbis 로 변환하고, 이미 압축된
//   (Vorbis) 클립은 건드리지 않는다 — 세대손실 없이 near-transparent. WebGL 에서 PCM 은 사실상
//   오설정이므로 Vorbis 로 정규화하는 것은 "명백한 낭비 교정"에 해당한다.
//   explicit 활성(audioReencode==1)에서는 이미 Vorbis 인 클립도 target quality 초과 시 낮춘다.
//
// 오디오 스트리밍과의 상호배제:
//   AITAudioStreamingProcessor 가 먼저(오케스트레이터 적용 순서상 선행) 대용량 클립을 무음 스텁으로
//   치환하고 원본을 <src>.aitstreambak 로 백업한다. 재인코딩은 그 백업이 존재하는(=외부화된) 클립을
//   건너뛴다 — 무음 스텁을 재인코딩하는 것은 무의미하고 스텁 치환 상태를 오염시킬 수 있기 때문이다.
//
// 비파괴: 각 클립의 원본 .meta 를 <path>.meta.aitaudioreencodebak 로 백업, 빌드 후 원본을 그대로
//   복원한다(자동으로 추가된 WebGL 오버라이드도 함께 제거됨). 빌드가 비정상 종료되어 복원이 누락돼도,
//   다음 에디터 로드 시 안전망(SafetyNetRestore)이 잔존 백업을 자동 복원한다.
//
// 통합: AITWebGLBuilder.BuildWebGL 가 BuildPipeline.BuildPlayer 직전(오디오 스트리밍 외부화 직후)에
//   ApplyForBuild, try/finally 의 finally 에서(오디오 스트리밍 복원 직전) RestoreForBuild 를 호출한다.
//   적용 순서: audio-stream → audio-reencode → crunch → …, 복원은 그 정확한 역순.
//
// ⚠ 비용: compressionFormat/quality 변경은 codec 재인코딩 reimport 를 유발한다(대상 수에 비례).
//   apply + 복원으로 2회 reimport 가 발생한다. 그럼에도 효익 실현을 위해 기본 ON 이며, 폴더 제외
//   (audioReencodeExcludeDirs)·값 0 으로 opt-out 가능하다.
//
// 버전 호환: preloadAudioData 는 Unity 6 에서 obsolete-as-error(CS0619)이고 2021.3 대체 필드가 없어
//   일절 건드리지 않는다. compressionFormat/quality(AudioImporterSampleSettings)만 override 한다.

using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AppsInToss.Editor
{
    /// <summary>
    /// 빌드 단계 오디오 재인코딩(Vorbis + quality) 처리기.
    /// <see cref="AITEditorScriptObject.audioReencode"/> 설정에 따라 동작한다.
    /// loadType/sampleRate/preloadAudioData 는 건드리지 않고 compressionFormat/quality 만 override 한다.
    /// 런타임 컴포넌트는 없다(빌드 산출물만 작아질 뿐, 런타임 동작 동일).
    /// </summary>
    [InitializeOnLoad]
    public static class AITAudioReencodeProcessor
    {
        /// <summary>원본 .meta 를 보관하는 백업 접미사.</summary>
        private const string BackupSuffix = ".aitaudioreencodebak";

        /// <summary>apply 가 진행 중임을 표시하는 마커(Unity 가 무시하는 '.' 접두 숨김 파일).</summary>
        private const string MarkerRelative = "Assets/.ait-audioreencode-active";

        /// <summary>오디오 스트리밍 외부화가 남기는 소스 백업 접미사(상호배제 판별용).</summary>
        private const string StreamBackupSuffix = ".aitstreambak";

        /// <summary>한 번의 재인코딩 적용 결과 핸들. finally 에서 정확한 복원에 사용.</summary>
        public sealed class ReencodeHandle
        {
            /// <summary>이번 빌드에서 재인코딩이 실제로 수행되었는지.</summary>
            public bool Active;

            /// <summary>처리된 클립 개수.</summary>
            public int ClipCount;
        }

        static AITAudioReencodeProcessor()
        {
            EditorApplication.delayCall += SafetyNetRestore;
        }

        /// <summary>
        /// 빌드 직전 호출: 설정이 켜져 있으면 대상 클립의 WebGL compressionFormat/quality 를 override 해 reimport 한다.
        /// </summary>
        /// <param name="config">프로젝트 에디터 설정. null 이거나 기능이 꺼져 있으면 no-op.</param>
        /// <returns>복원에 사용할 핸들(항상 non-null).</returns>
        public static ReencodeHandle ApplyForBuild(AITEditorScriptObject config)
        {
            var handle = new ReencodeHandle();

            // tri-state 게이트: -1 = 자동(AITDefaultSettings 기본값), 0 = 비활성, 1 = 활성.
            bool enabled = config != null && (config.audioReencode >= 0
                ? config.audioReencode == 1
                : AITDefaultSettings.GetDefaultAudioReencode());
            if (!enabled)
            {
                return handle;
            }

            try
            {
                bool explicitMode = config.audioReencode == 1; // explicit ON 에서만 기존 Vorbis 도 낮춤.
                float quality = Mathf.Clamp01(config.audioReencodeQuality);
                long minBytes = config.audioReencodeMinBytes > 0 ? config.audioReencodeMinBytes : 0; // 0 = 필터 없음
                string[] dirs = SplitDirs(config.audioReencodeDirs);              // null = 전체 Assets
                string[] excludeDirs = SplitDirs(config.audioReencodeExcludeDirs); // 사용자 escape hatch

                CreateMarker();

                int clips = ApplyAudioClips(quality, explicitMode, minBytes, dirs, excludeDirs);

                AssetDatabase.Refresh();

                handle.Active = clips > 0;
                handle.ClipCount = clips;
                if (!handle.Active)
                {
                    // 대상 0건 → 마커 제거(복원할 것 없음).
                    RemoveMarker();
                }

                Debug.Log($"[AIT-AudioReencode] ✓ 오디오 {clips}개 Vorbis q{quality:0.00} 재인코딩 적용{(config.audioReencode < 0 ? " (자동)" : "")} (loadType/sampleRate 불변).");
                return handle;
            }
            catch (Exception e)
            {
                Debug.LogError($"[AIT-AudioReencode] 적용 예외 → 복원 후 건너뜀: {e}");
                RestoreAllBackups();
                RemoveMarker();
                AssetDatabase.Refresh();
                return new ReencodeHandle();
            }
        }

        /// <summary>빌드 종료 후(성공/실패 무관) 호출: 원본 임포트 설정으로 복원한다.</summary>
        public static void RestoreForBuild(ReencodeHandle handle)
        {
            if (handle == null || !handle.Active)
            {
                return;
            }

            try
            {
                int restored = RestoreAllBackups();
                RemoveMarker();
                AssetDatabase.Refresh();
                Debug.Log($"[AIT-AudioReencode] 복원 완료: {restored}개 오디오 원본 임포트 설정 원상.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[AIT-AudioReencode] 복원 예외: {e}");
            }
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
                RemoveMarker();
                if (restored > 0)
                {
                    AssetDatabase.Refresh();
                    Debug.LogWarning($"[AIT-AudioReencode] 안전망: 이전 빌드 잔존 백업 {restored}개를 복원했습니다.");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT-AudioReencode] 안전망 복원 중 예외(무시): {e}");
            }
        }

        // ─────────────────────────── AudioClip ───────────────────────────
        private static int ApplyAudioClips(float quality, bool explicitMode, long minBytes, string[] dirs, string[] excludeDirs)
        {
            int n = 0;
            var guids = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets" });
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            foreach (var g in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/"))
                {
                    continue;
                }

                // 다른 처리기의 StreamingAssets 사본은 건드리지 않음(자기 외부화본 보호).
                if (path.IndexOf("/ait-stream-", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }

                if (dirs != null && !UnderAny(path, dirs))
                {
                    continue;
                }

                if (excludeDirs != null && UnderAny(path, excludeDirs))
                {
                    continue; // 사용자 escape hatch
                }

                string srcFull = Path.Combine(projectRoot, path);

                // 상호배제: 오디오 스트리밍이 이 클립을 외부화(무음 스텁 치환)했다면 건너뛴다.
                // 스트리밍은 오케스트레이터 적용 순서상 선행하므로 이 시점에 백업이 존재한다.
                if (File.Exists(srcFull + StreamBackupSuffix))
                {
                    continue;
                }

                var ti = AssetImporter.GetAtPath(path) as AudioImporter;
                if (ti == null)
                {
                    continue;
                }

                // 빌드가 실제로 ship 하는 설정을 해석한다: WebGL 오버라이드가 있으면 그것을, 없으면 base(default).
                bool overridden = ti.ContainsSampleSettingsOverride("WebGL");
                AudioImporterSampleSettings cur = overridden
                    ? ti.GetOverrideSampleSettings("WebGL")
                    : ti.defaultSampleSettings;

                if (!NeedsReencode(cur.compressionFormat, cur.quality, quality, explicitMode))
                {
                    continue; // 이미 Vorbis(자동)·target 이하 등 → 줄일 것 없음.
                }

                // 바이트 필터(선택): 소스 파일이 minBytes 미만이면 제외(짧은 SFX 보호).
                if (minBytes > 0)
                {
                    if (!File.Exists(srcFull) || new FileInfo(srcFull).Length < minBytes)
                    {
                        continue;
                    }
                }

                // 원본 .meta 백업(verbatim 복원용).
                if (!BackupAssetMeta(path, projectRoot))
                {
                    continue;
                }

                // compressionFormat/quality 만 override. loadType/sampleRate 등은 base 에서 승계(보존).
                // WebGL 오버라이드로 세팅해 빌드가 실제로 Vorbis 를 ship 하도록 강제한다(base 만 바꾸면
                // 기존 WebGL 오버라이드가 우선해 무효가 될 수 있음).
                AudioImporterSampleSettings ns = overridden
                    ? ti.GetOverrideSampleSettings("WebGL")
                    : ti.defaultSampleSettings;
                ns.compressionFormat = AudioCompressionFormat.Vorbis;
                ns.quality = quality;
                ti.SetOverrideSampleSettings("WebGL", ns);
                ti.SaveAndReimport();
                n++;
            }

            return n;
        }

        /// <summary>
        /// 재인코딩 필요 여부(순수 판정). 자동/explicit 공통으로 비압축(PCM/ADPCM)은 Vorbis 로 변환한다
        /// (원본이 비압축이라 Vorbis 화의 실효 손실이 작음). 이미 Vorbis 인 클립은 explicit 모드에서만,
        /// 그리고 현재 quality 가 target 을 초과할 때만 낮춘다(자동 모드는 기존 Vorbis 를 건드리지 않음 →
        /// 세대손실 방지, 작성자 의도 존중). MP3/AAC 등 기타 압축 포맷은 어느 모드에서도 건드리지 않는다.
        /// </summary>
        internal static bool NeedsReencode(AudioCompressionFormat currentFormat, float currentQuality, float targetQuality, bool explicitMode)
        {
            if (currentFormat == AudioCompressionFormat.PCM || currentFormat == AudioCompressionFormat.ADPCM)
            {
                return true; // 비압축/경량압축 → Vorbis 로 변환(가장 큰 이득).
            }

            if (currentFormat == AudioCompressionFormat.Vorbis && explicitMode && currentQuality > targetQuality + 0.001f)
            {
                return true; // 이미 Vorbis 이지만 explicit 모드에서 target 초과분을 낮춤.
            }

            return false; // 이미 압축(Vorbis≤target, MP3/AAC 등) → 건드리지 않음.
        }

        // ─────────────────────────── 백업/복원 ───────────────────────────

        /// <summary>에셋의 .meta 파일을 <path>.meta.aitaudioreencodebak 로 백업.</summary>
        private static bool BackupAssetMeta(string assetPath, string projectRoot)
        {
            try
            {
                string metaFull = Path.Combine(projectRoot, assetPath + ".meta");
                if (!File.Exists(metaFull))
                {
                    return false;
                }

                string bak = metaFull + BackupSuffix;
                if (!File.Exists(bak))
                {
                    File.Copy(metaFull, bak, true);
                }

                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT-AudioReencode] .meta 백업 실패({assetPath}): {e.Message}");
                return false;
            }
        }

        /// <summary>Assets 트리의 모든 *.aitaudioreencodebak 를 원본으로 되돌리고 백업을 삭제한다. 복원 개수 반환.</summary>
        private static int RestoreAllBackups()
        {
            int restored = 0;
            string assetsPath = Application.dataPath;
            string projectRoot = Directory.GetParent(assetsPath).FullName;
            string[] backups;
            try
            {
                backups = Directory.GetFiles(assetsPath, "*" + BackupSuffix, SearchOption.AllDirectories);
            }
            catch
            {
                return 0;
            }

            foreach (var bak in backups)
            {
                // bak = "<original>.meta.aitaudioreencodebak" → original 은 오디오의 .meta.
                string original = bak.Substring(0, bak.Length - BackupSuffix.Length);
                try
                {
                    File.Copy(bak, original, true);
                    File.Delete(bak);

                    // reimport 대상 에셋 경로 산출: original 은 .meta 이므로 본체 경로로 환원.
                    string assetFull = original.EndsWith(".meta")
                        ? original.Substring(0, original.Length - ".meta".Length)
                        : original;
                    string rel = AbsoluteToProjectRelative(assetFull, projectRoot);
                    if (!string.IsNullOrEmpty(rel))
                    {
                        AssetDatabase.ImportAsset(rel, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                    }

                    restored++;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[AIT-AudioReencode] 백업 복원 실패({bak}): {e.Message}");
                }
            }

            return restored;
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
    }
}
