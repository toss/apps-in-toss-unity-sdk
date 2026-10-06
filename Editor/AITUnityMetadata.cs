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
            var pairs = GetContentMetadataPairs();
            // 빌드(패키징) 시각 — .ait 생성 시점. 콘솔 배포(deploy) 시각과는 다르다.
            pairs.Add($"\"buildTimestamp\":{JsonEscape(DateTime.UtcNow.ToString("o"))}");
            return "{" + string.Join(",", pairs) + "}";
        }

        /// <summary>
        /// UNITY_METADATA 중 buildTimestamp를 제외한 나머지 필드만의 compact JSON.
        /// buildTimestamp는 매 빌드마다 항상 달라지므로, 이 값을 그대로
        /// Package.PackageBuildStateMarker의 스킵 판정 해시 입력에 쓰면 스킵이 영원히
        /// 발동하지 않게 된다 — sdkVersion/sdkCommitHash/unityVersion처럼 .ait 헤더에
        /// 그대로 반영되지만 "빌드 내용"의 일부인 필드만 골라 해시하기 위한 전용 메서드.
        /// </summary>
        internal static string BuildContentMetadataJson()
        {
            return "{" + string.Join(",", GetContentMetadataPairs()) + "}";
        }

        private static List<string> GetContentMetadataPairs()
        {
            return new List<string>
            {
                $"\"unityVersion\":{JsonEscape(Application.unityVersion)}",
                $"\"bundleVersion\":{JsonEscape(PlayerSettings.bundleVersion)}",
                $"\"sdkVersion\":{JsonEscape(AITVersion.Version)}",
                $"\"sdkCommitHash\":{JsonEscape(ResolveSdkCommitHash())}",
                $"\"productName\":{JsonEscape(PlayerSettings.productName)}",
                $"\"companyName\":{JsonEscape(PlayerSettings.companyName)}",
            };
        }

        /// <summary>
        /// SDK 커밋 해시를 결정합니다. 릴리즈 태그/UPM git.hash/packages-lock 경로(<see cref="AITVersion.CommitHash"/>)를
        /// 우선하고, 그 어디에도 값이 없는 로컬·embedded 설치에서는 SDK 패키지의 git 저장소를 직접 조회하는
        /// <see cref="AITSdkCommitResolver"/> 폴백을 사용합니다. 둘 다 실패하면 빈 문자열.
        /// </summary>
        private static string ResolveSdkCommitHash()
        {
            string hash = AITVersion.CommitHash;
            if (!string.IsNullOrEmpty(hash))
                return hash;
            return AITSdkCommitResolver.TryResolveLocalGitCommitHash() ?? "";
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
