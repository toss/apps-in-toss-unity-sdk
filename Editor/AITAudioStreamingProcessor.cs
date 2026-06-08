// -----------------------------------------------------------------------
// <copyright file="AITAudioStreamingProcessor.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Streaming Audio (build-time externalizer)
// </copyright>
// -----------------------------------------------------------------------
//
// 빌드 직전, 대용량 AudioClip을 초기 .data 밖(StreamingAssets)으로 빼내고
// 프로젝트 내 소스는 무음 스텁으로 치환한다. 런타임(AITStreamingAudio)이 매니페스트를
// 보고 interactive 이후 실 오디오를 스트리밍 로드하여 재생을 복원한다.
//
// 효과: Unity는 씬/직렬화 배열이 참조하는 AudioClip을 .data에 굽는다.
//   소스를 짧은 무음으로 치환하면 .data의 해당 바이트가 사라져 초기 다운로드/TTI가 급감.
//   원본(스트리밍 재생용)은 StreamingAssets/ait-stream-audio/<guid>.<ext> 로 동봉(온디맨드).
//   GUID/.meta를 보존하므로 직렬화 배열·씬 참조가 전부 유효(클립 객체는 존재, 데이터만 무음).
//
// 비파괴: 소스 원본은 <src>.aitstreambak 로 백업, 빌드 종료(성공/실패 무관) 시 원상 복원한다.
//   빌드 프로세스가 비정상 종료되어 복원이 누락되어도, 다음 에디터 로드 시 안전망
//   (SafetyNetRestore)이 잔존 백업을 자동 복원한다.
//
// 통합: AITConvertCore.BuildWebGL 가 BuildPipeline.BuildPlayer 직전에 ExternalizeForBuild,
//   try/finally 의 finally 에서 RestoreForBuild 를 호출한다(버전 JSON write/remove 와 동일 패턴).

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AppsInToss.Editor
{
    /// <summary>
    /// 빌드 단계 오디오 외부화/복원 처리기. <see cref="AITEditorScriptObject.enableAudioStreaming"/>
    /// 설정에 따라 동작하며, 런타임 컴포넌트 <c>AppsInToss.AITStreamingAudio</c> 와 짝을 이룬다.
    /// </summary>
    [InitializeOnLoad]
    public static class AITAudioStreamingProcessor
    {
        /// <summary>외부화된 오디오/매니페스트가 놓이는 프로젝트 상대 경로.</summary>
        private const string StreamRootAssets = "Assets/StreamingAssets/ait-stream-audio";

        /// <summary>치환 전 원본 소스를 보관하는 백업 접미사.</summary>
        private const string BackupSuffix = ".aitstreambak";

        /// <summary>SDK 패키지에 동봉된 무음 스텁 디렉토리(Unity 미임포트: '~' 접미사).</summary>
        private const string SilentStubDirName = "SilentStubs~";

        /// <summary>한 번의 외부화 결과를 나타내는 핸들. finally 에서 정확한 복원에 사용.</summary>
        public sealed class StreamHandle
        {
            /// <summary>이번 빌드에서 외부화가 실제로 수행되었는지.</summary>
            public bool Active;

            /// <summary>외부화된 오디오 개수.</summary>
            public int Count;
        }

        static AITAudioStreamingProcessor()
        {
            // 에디터 로드 시 안전망: 이전 빌드가 비정상 종료되어 복원이 누락된 경우 자동 복원.
            // streamroot 가 남아 있으면(=정상 빌드라면 제거됐어야 함) 잔존 백업을 원상 복원한다.
            EditorApplication.delayCall += SafetyNetRestore;
        }

        /// <summary>
        /// 빌드 직전 호출: 설정이 켜져 있으면 대용량 오디오를 외부화하고 소스를 무음 스텁으로 치환한다.
        /// AssetDatabase 를 동기 갱신하므로 이후 BuildPipeline.BuildPlayer 가 스텁본을 패키징한다.
        /// </summary>
        /// <param name="config">프로젝트 에디터 설정. null 이거나 기능이 꺼져 있으면 no-op.</param>
        /// <returns>복원에 사용할 핸들(항상 non-null).</returns>
        public static StreamHandle ExternalizeForBuild(AITEditorScriptObject config)
        {
            var handle = new StreamHandle();
            if (config == null || !config.enableAudioStreaming)
            {
                return handle;
            }

            try
            {
                long minBytes = config.audioStreamingMinBytes > 0 ? config.audioStreamingMinBytes : 262144;
                string[] dirs = SplitDirs(config.audioStreamingDirs);

                string silentDir = ResolveSilentStubDir();
                if (string.IsNullOrEmpty(silentDir) || !Directory.Exists(silentDir))
                {
                    Debug.LogWarning($"[AIT-StreamingAudio] 무음 스텁 디렉토리를 찾지 못해 외부화를 건너뜁니다: '{silentDir}'");
                    return handle;
                }

                string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                Directory.CreateDirectory(Path.Combine(projectRoot, StreamRootAssets));

                var entries = new List<string>();
                int n = 0;
                long stubbedBytes = 0;
                var guids = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets" });
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

                    string ext = Path.GetExtension(path).ToLowerInvariant(); // ".mp3" 등(점 포함)
                    string silent = Path.Combine(silentDir, "silent" + ext);
                    if (!File.Exists(silent))
                    {
                        Debug.LogWarning($"[AIT-StreamingAudio] 무음 스텁 없음({ext}) → 건너뜀: {path}");
                        continue;
                    }

                    // 실 길이 캡처(매니페스트 정보용; 런타임 판정은 스텁 length<0.5s 기준이라 부차적).
                    float realLen = 0f;
                    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                    if (clip != null)
                    {
                        realLen = clip.length;
                    }

                    string clipName = Path.GetFileNameWithoutExtension(path);

                    // 1) 원본 → StreamingAssets/<guid><ext> (온디맨드 스트리밍 소스)
                    string streamFile = g + ext;
                    File.Copy(srcFull, Path.Combine(projectRoot, StreamRootAssets, streamFile), true);

                    // 2) 소스 백업 + 무음 치환 + reimport (.data 에서 제거)
                    string bak = srcFull + BackupSuffix;
                    if (!File.Exists(bak))
                    {
                        File.Copy(srcFull, bak, true);
                    }

                    File.Copy(silent, srcFull, true);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);

                    entries.Add("{\"guid\":\"" + g + "\",\"name\":" + JsonStr(clipName)
                                + ",\"file\":" + JsonStr(streamFile) + ",\"length\":" + realLen.ToString("0.###") + "}");
                    n++;
                    stubbedBytes += size;
                    Debug.Log($"[AIT-StreamingAudio]   외부화 {clipName} ({size / 1048576f:0.00}MB src, len {realLen:0.0}s) → {streamFile}");
                }

                // 3) 매니페스트 동봉
                var sb = new StringBuilder();
                sb.Append("{\"entries\":[").Append(string.Join(",", entries)).Append("]}");
                string manifestPath = Path.Combine(projectRoot, StreamRootAssets, "manifest.json");
                File.WriteAllText(manifestPath, sb.ToString());
                AssetDatabase.Refresh();

                handle.Active = true;
                handle.Count = n;
                Debug.Log($"[AIT-StreamingAudio] ✓ {n}개 오디오 외부화, 소스 {stubbedBytes / 1048576f:0.0}MB 스텁(초기 .data 제거).");
                return handle;
            }
            catch (Exception e)
            {
                // 외부화 실패가 빌드 전체를 막지 않도록: 부분 변경을 즉시 복원하고 비활성 핸들 반환.
                Debug.LogError($"[AIT-StreamingAudio] 외부화 예외 → 복원 후 건너뜀: {e}");
                RestoreAllBackups();
                RemoveStreamRoot();
                AssetDatabase.Refresh();
                return new StreamHandle();
            }
        }

        /// <summary>
        /// 빌드 종료 후(성공/실패 무관) 호출: 무음 스텁을 원본으로 복원하고 StreamingAssets 사본/매니페스트를 제거한다.
        /// </summary>
        public static void RestoreForBuild(StreamHandle handle)
        {
            if (handle == null || !handle.Active)
            {
                return;
            }

            try
            {
                int restored = RestoreAllBackups();
                RemoveStreamRoot();
                AssetDatabase.Refresh();
                Debug.Log($"[AIT-StreamingAudio] 복원 완료: {restored}개 소스 원상, StreamingAssets 사본 제거");
            }
            catch (Exception e)
            {
                Debug.LogError($"[AIT-StreamingAudio] 복원 예외: {e}");
            }
        }

        /// <summary>
        /// 에디터 로드 시 안전망. streamroot 가 잔존하면(=이전 빌드가 복원 전에 종료) 백업을 자동 복원한다.
        /// 정상 빌드 후에는 streamroot 가 제거되므로 이 경로는 비용 없이 즉시 반환된다.
        /// </summary>
        private static void SafetyNetRestore()
        {
            try
            {
                string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                string streamRootFull = Path.Combine(projectRoot, StreamRootAssets);
                if (!Directory.Exists(streamRootFull))
                {
                    return; // 공통 경로: 잔존물 없음
                }

                int restored = RestoreAllBackups();
                RemoveStreamRoot();
                if (restored > 0)
                {
                    AssetDatabase.Refresh();
                    Debug.LogWarning($"[AIT-StreamingAudio] 안전망: 이전 빌드 잔존 백업 {restored}개를 복원했습니다.");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT-StreamingAudio] 안전망 복원 중 예외(무시): {e}");
            }
        }

        /// <summary>Assets 트리의 모든 *.aitstreambak 를 원본으로 되돌리고 백업을 삭제한다. 복원 개수 반환.</summary>
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
                string srcFull = bak.Substring(0, bak.Length - BackupSuffix.Length);
                try
                {
                    File.Copy(bak, srcFull, true);
                    File.Delete(bak);

                    string rel = AbsoluteToProjectRelative(srcFull, projectRoot);
                    if (!string.IsNullOrEmpty(rel))
                    {
                        AssetDatabase.ImportAsset(rel, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                    }

                    restored++;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[AIT-StreamingAudio] 백업 복원 실패({bak}): {e.Message}");
                }
            }

            return restored;
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
                Debug.LogWarning($"[AIT-StreamingAudio] StreamingAssets 사본 제거 실패: {e.Message}");
            }
        }

        /// <summary>SDK 패키지에 동봉된 무음 스텁 디렉토리 절대경로. UPM/embedded 설치 모두 해석.</summary>
        private static string ResolveSilentStubDir()
        {
            try
            {
                var pkg = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(AITAudioStreamingProcessor).Assembly);
                if (pkg != null && !string.IsNullOrEmpty(pkg.resolvedPath))
                {
                    return Path.Combine(pkg.resolvedPath, "Editor", SilentStubDirName);
                }
            }
            catch
            {
                // PackageInfo 미해석(예: Assets 내 임베드 개발) → 소스 파일 위치 폴백
            }

            string here = CallerDir();
            return string.IsNullOrEmpty(here) ? null : Path.Combine(here, SilentStubDirName);
        }

        private static string CallerDir([System.Runtime.CompilerServices.CallerFilePath] string thisFile = "")
            => string.IsNullOrEmpty(thisFile) ? null : Path.GetDirectoryName(thisFile);

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
    }
}
