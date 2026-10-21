using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using AppsInToss.Editor;

namespace AppsInToss
{
    /// <summary>
    /// 유틸리티 클래스
    /// </summary>
    public static class UnityUtil
    {
        public static string GetProjectPath()
        {
            return Directory.GetParent(Application.dataPath).FullName;
        }

        public static string[] GetBuildScenes()
        {
            var scenes = new List<string>();
            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled)
                {
                    scenes.Add(scene.path);
                }
            }
            return scenes.ToArray();
        }

        public static AITEditorScriptObject GetEditorConf()
        {
            string configPath = "Assets/AppsInToss/Editor/AITConfig.asset";
            var config = AssetDatabase.LoadAssetAtPath<AITEditorScriptObject>(configPath);

            if (config == null)
            {
                // 기본 설정 생성
                config = ScriptableObject.CreateInstance<AITEditorScriptObject>();

                string directory = Path.GetDirectoryName(configPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                AssetDatabase.CreateAsset(config, configPath);
                AssetDatabase.SaveAssets();
            }

            return config;
        }

        /// <summary>
        /// 소스 디렉토리를 대상 디렉토리로 재귀 복사 (.meta 파일 제외)
        /// </summary>
        public static void CopyDirectory(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                // .meta 파일은 SDK GUID 충돌 방지를 위해 복사하지 않음 (Unity가 대상 위치에 새로 생성)
                if (IsMetaFile(file))
                    continue;

                string targetFile = Path.Combine(targetDir, Path.GetFileName(file));
                File.Copy(file, targetFile, true);
                // Unity WebGL 빌드 파일(.unityweb)이 600 권한으로 생성되는 문제 해결
                // 복사 후 읽기 권한 부여 (웹 서버에서 접근 가능하도록)
                EnsureFileReadable(targetFile);
            }

            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                string targetSubDir = Path.Combine(targetDir, Path.GetFileName(dir));
                CopyDirectory(dir, targetSubDir);
            }
        }

        /// <summary>
        /// 두 디렉토리의 내용이 동일한지 비교 (파일명, 크기 기준, .meta 파일 제외)
        /// </summary>
        public static bool DirectoriesEqual(string dir1, string dir2)
        {
            if (!Directory.Exists(dir1) || !Directory.Exists(dir2))
                return false;

            var files1 = Directory.GetFiles(dir1, "*", SearchOption.AllDirectories)
                .Where(f => !IsMetaFile(f)).ToArray();
            var files2 = Directory.GetFiles(dir2, "*", SearchOption.AllDirectories)
                .Where(f => !IsMetaFile(f)).ToArray();

            if (files1.Length != files2.Length)
                return false;

            // 상대 경로 기준으로 비교
            var set = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var f in files1)
            {
                string rel = f.Substring(dir1.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                set.Add(rel);
            }

            foreach (var f in files2)
            {
                string rel = f.Substring(dir2.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (!set.Contains(rel))
                    return false;

                string f1 = Path.Combine(dir1, rel);
                var info1 = new FileInfo(f1);
                var info2 = new FileInfo(f);
                if (info1.Length != info2.Length)
                    return false;
            }

            return true;
        }

        private static bool IsMetaFile(string path)
            => path.EndsWith(".meta", System.StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// 파일에 읽기 권한 부여 (macOS/Linux에서 chmod 644 효과)
        /// </summary>
        internal static void EnsureFileReadable(string filePath)
        {
            try
            {
                // Windows: 읽기 전용 속성 제거
                var attributes = File.GetAttributes(filePath);
                if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                {
                    File.SetAttributes(filePath, attributes & ~FileAttributes.ReadOnly);
                }

                // macOS/Linux: Mono.Posix가 없어도 동작하도록 chmod 명령 사용
#if UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX
                try
                {
                    var process = new System.Diagnostics.Process
                    {
                        StartInfo = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "chmod",
                            Arguments = $"644 \"{filePath}\"",
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true
                        }
                    };
                    process.Start();
                    process.WaitForExit(1000);
                }
                catch (System.Exception ex)
                {
                    // best-effort 권한 조정이므로 Sentry 전송 억제
                    AITLog.Warning($"[AIT] chmod 실행 실패: {filePath} ({ex.GetType().Name}: {ex.Message})", sentryCapture: false);
                }
#endif
            }
            catch (System.Exception ex)
            {
                // best-effort 권한 조정이므로 Sentry 전송 억제
                AITLog.Warning($"[AIT] 파일 권한 설정 실패: {filePath} ({ex.GetType().Name}: {ex.Message})", sentryCapture: false);
            }
        }
    }
}

namespace AppsInToss.Editor
{
    /// <summary>
    /// 파일 시스템 유틸리티 클래스
    /// </summary>
    internal static class AITFileUtils
    {
        /// <summary>
        /// 디렉토리 안전 삭제 (읽기 전용 속성 제거 후 재귀 삭제).
        /// 내부적으로 <see cref="AITFileSystemHelper.SafeDeleteDirectory"/>에 위임합니다.
        /// </summary>
        /// <returns>삭제 성공 또는 디렉토리 부재 시 true, 실패 시 false</returns>
        internal static bool DeleteDirectory(string path)
        {
            return AITFileSystemHelper.SafeDeleteDirectory(path);
        }

        /// <summary>
        /// 디렉토리 크기 계산
        /// </summary>
        internal static long GetDirectorySize(string path)
        {
            if (!Directory.Exists(path))
                return 0;

            long size = 0;
            try
            {
                var files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        size += fileInfo.Length;
                    }
                    catch (System.Exception ex)
                    {
                        // 크기 조회 실패는 통계 집계 정확도만 떨어뜨리므로 Sentry 전송 억제
                        AITLog.Warning($"[AIT] 파일 크기 조회 실패: {file} ({ex.GetType().Name}: {ex.Message})", sentryCapture: false);
                    }
                }
            }
            catch (System.Exception ex)
            {
                AITLog.Warning($"[AIT] 디렉토리 열람 실패: {path} ({ex.GetType().Name}: {ex.Message})", sentryCapture: false);
            }

            return size;
        }
    }
}
