using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

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

        public static void CopyDirectory(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                string targetFile = Path.Combine(targetDir, Path.GetFileName(file));
                File.Copy(file, targetFile, true);
            }

            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                string targetSubDir = Path.Combine(targetDir, Path.GetFileName(dir));
                CopyDirectory(dir, targetSubDir);
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
        /// 디렉토리 안전 삭제 (읽기 전용 속성 제거 후 삭제)
        /// </summary>
        internal static void DeleteDirectory(string path)
        {
            if (!Directory.Exists(path))
                return;

            // 모든 파일의 읽기 전용 속성 제거
            foreach (string file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
            {
                try
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                    File.Delete(file);
                }
                catch { }
            }

            // 모든 하위 폴더 삭제
            foreach (string dir in Directory.GetDirectories(path, "*", SearchOption.AllDirectories))
            {
                try
                {
                    Directory.Delete(dir, false);
                }
                catch { }
            }

            // 최상위 폴더 삭제
            try
            {
                Directory.Delete(path, true);
            }
            catch { }
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
                    catch { }
                }
            }
            catch { }

            return size;
        }
    }
}
