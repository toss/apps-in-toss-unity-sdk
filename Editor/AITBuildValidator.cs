using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AppsInToss
{
    /// <summary>
    /// Apps in Toss 빌드 전 검증 클래스
    /// </summary>
    public static class AITBuildValidator
    {
        /// <summary>
        /// 빌드 전 필수 조건 검증
        /// </summary>
        /// <returns>검증 결과 (에러 메시지 리스트)</returns>
        public static List<string> ValidateBeforeBuild()
        {
            var errors = new List<string>();
            var config = UnityUtil.GetEditorConf();

            // 1. 앱 설정 검증
            if (string.IsNullOrWhiteSpace(config.appName))
            {
                errors.Add("앱 ID가 설정되지 않았습니다.");
            }

            if (string.IsNullOrWhiteSpace(config.iconUrl))
            {
                errors.Add("아이콘 URL이 설정되지 않았습니다. (필수)");
            }
            else if (!IsValidUrl(config.iconUrl))
            {
                errors.Add("아이콘 URL 형식이 올바르지 않습니다. (http:// 또는 https://로 시작해야 함)");
            }

            if (string.IsNullOrWhiteSpace(config.version))
            {
                errors.Add("앱 버전이 설정되지 않았습니다.");
            }

            // 2. Node.js 검증은 제거 (빌드 시 Portable Node.js 자동 다운로드)
            // SDK가 필요시 자동으로 Embedded Node.js를 다운로드하므로 사전 검증 불필요

            // 3. WebGL Build Support 검증
            if (!IsWebGLBuildSupportInstalled())
            {
                errors.Add("WebGL Build Support가 설치되지 않았습니다. Unity Hub에서 설치해주세요.");
            }

            // 4. 디스크 공간 검증 (최소 2GB)
            if (!HasEnoughDiskSpace(2048)) // 2GB in MB
            {
                errors.Add("디스크 공간이 부족합니다. 최소 2GB의 여유 공간이 필요합니다.");
            }

            return errors;
        }

        /// <summary>
        /// 배포 전 검증
        /// </summary>
        public static List<string> ValidateBeforeDeploy()
        {
            var errors = new List<string>();
            var config = UnityUtil.GetEditorConf();

            // 1. 배포 키 검증
            if (string.IsNullOrWhiteSpace(config.deploymentKey))
            {
                errors.Add("배포 키가 설정되지 않았습니다.");
            }

            // 2. 빌드 결과물 존재 확인
            string projectPath = UnityUtil.GetProjectPath();
            string distPath = Path.Combine(projectPath, "ait-build", "dist");

            if (!Directory.Exists(distPath))
            {
                errors.Add("빌드 결과물이 없습니다. 먼저 빌드를 실행해주세요.");
            }
            else
            {
                // 3. 빌드 크기 검증 (최대 100MB 권장)
                long buildSize = GetDirectorySize(distPath);
                long maxSize = 100 * 1024 * 1024; // 100MB

                if (buildSize > maxSize)
                {
                    errors.Add($"빌드 크기가 너무 큽니다: {buildSize / 1024 / 1024}MB (권장: 100MB 이하)");
                }
            }

            return errors;
        }

        /// <summary>
        /// URL 형식 검증
        /// </summary>
        private static bool IsValidUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            return url.StartsWith("http://") || url.StartsWith("https://");
        }

        /// <summary>
        /// WebGL Build Support 설치 여부 확인
        /// </summary>
        private static bool IsWebGLBuildSupportInstalled()
        {
            // Unity의 BuildTarget이 WebGL을 지원하는지 확인
            return BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL);
        }

        /// <summary>
        /// 디스크 여유 공간 확인
        /// </summary>
        private static bool HasEnoughDiskSpace(long requiredMB)
        {
            try
            {
                string projectPath = UnityUtil.GetProjectPath();
                DriveInfo drive = new DriveInfo(Path.GetPathRoot(projectPath));
                long availableMB = drive.AvailableFreeSpace / 1024 / 1024;

                return availableMB >= requiredMB;
            }
            catch
            {
                // 확인 실패 시 true 반환 (빌드 시도는 허용)
                return true;
            }
        }

        /// <summary>
        /// 디렉토리 크기 계산
        /// </summary>
        private static long GetDirectorySize(string path)
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

        /// <summary>
        /// 검증 결과를 사용자 친화적 메시지로 포맷
        /// </summary>
        public static string FormatValidationErrors(List<string> errors)
        {
            if (errors.Count == 0)
                return string.Empty;

            var message = "다음 문제를 해결해주세요:\n\n";
            for (int i = 0; i < errors.Count; i++)
            {
                message += $"{i + 1}. {errors[i]}\n";
            }

            return message;
        }
    }
}
