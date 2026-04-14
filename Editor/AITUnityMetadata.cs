using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AppsInToss.Editor
{
    /// <summary>
    /// Unity 빌드 메타데이터를 수집하여 UNITY_METADATA 환경변수용 JSON을 생성합니다.
    /// granite build가 이 환경변수를 읽어 .ait 파일의 protobuf 헤더에 포함시킵니다.
    /// </summary>
    internal static class AITUnityMetadata
    {
        /// <summary>
        /// Unity 빌드 메타데이터를 compact JSON 문자열로 반환합니다.
        /// 환경변수로 전달되므로 줄바꿈 없는 single-line JSON이어야 합니다.
        /// (MiniJson.Serialize는 pretty-print하여 PowerShell 이스케이프 문제 발생)
        /// </summary>
        internal static string BuildMetadataJson()
        {
            var pairs = new string[]
            {
                $"\"unityVersion\":{JsonEscape(Application.unityVersion)}",
                $"\"bundleVersion\":{JsonEscape(PlayerSettings.bundleVersion)}",
                $"\"sdkVersion\":{JsonEscape(AITVersion.Version)}",
                $"\"sdkCommitHash\":{JsonEscape(AITVersion.CommitHash ?? "")}",
                $"\"productName\":{JsonEscape(PlayerSettings.productName)}",
                $"\"companyName\":{JsonEscape(PlayerSettings.companyName)}",
                $"\"buildTimestamp\":{JsonEscape(DateTime.UtcNow.ToString("o"))}",
            };
            return "{" + string.Join(",", pairs) + "}";
        }

        private static string JsonEscape(string value)
        {
            return "\"" + value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t") + "\"";
        }

        /// <summary>
        /// granite build에 전달할 환경변수 딕셔너리를 반환합니다.
        /// </summary>
        internal static Dictionary<string, string> BuildEnvironmentVariables()
        {
            return new Dictionary<string, string>
            {
                { "UNITY_METADATA", BuildMetadataJson() }
            };
        }
    }
}
