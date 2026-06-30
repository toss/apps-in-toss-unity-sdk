using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AppsInToss.Editor
{
    /// <summary>
    /// Unity 빌드 메타데이터를 수집하여 UNITY_METADATA 환경변수용 JSON을 생성합니다.
    /// granite build가 이 환경변수를 읽어 .ait 파일의 protobuf 헤더에 포함시킵니다.
    ///
    /// prefetch 카탈로그(metadata.extra.unityMetaData.prefetch)는 여기서 만들지 않습니다.
    /// 실제 Build/* 파일 크기·인코딩이 필요한 인라인 카탈로그는 WebGL 복사가 끝난 뒤
    /// <see cref="Package.AITWarmManifestEmitter.WriteManifest"/> 가 산출한 compact JSON 을
    /// <see cref="WithPrefetch"/> 로 메타데이터에 끼워 넣습니다(post-copy 패치).
    /// </summary>
    internal static class AITUnityMetadata
    {
        /// <summary>
        /// Unity 빌드 메타데이터를 compact JSON 문자열로 반환합니다(prefetch 선언 없음).
        /// 환경변수로 전달되므로 줄바꿈 없는 single-line JSON이어야 합니다.
        /// (MiniJson.Serialize는 pretty-print하여 PowerShell 이스케이프 문제 발생)
        /// </summary>
        internal static string BuildMetadataJson()
        {
            var pairs = new List<string>
            {
                $"\"unityVersion\":{JsonEscape(Application.unityVersion)}",
                $"\"bundleVersion\":{JsonEscape(PlayerSettings.bundleVersion)}",
                $"\"sdkVersion\":{JsonEscape(AITVersion.Version)}",
                $"\"sdkCommitHash\":{JsonEscape(ResolveSdkCommitHash())}",
                $"\"productName\":{JsonEscape(PlayerSettings.productName)}",
                $"\"companyName\":{JsonEscape(PlayerSettings.companyName)}",
                // 빌드(패키징) 시각 — .ait 생성 시점. 콘솔 배포(deploy) 시각과는 다르다.
                $"\"buildTimestamp\":{JsonEscape(DateTime.UtcNow.ToString("o"))}",
                // 번들 마킹 — 이 SDK 변형(perf 채널 등)으로 생성된 번들을 .ait 헤더에서 식별.
                $"\"buildVariant\":{JsonEscape(AITBuildVariant.Value)}",
            };

            return "{" + string.Join(",", pairs) + "}";
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

        /// <summary>
        /// 기존 메타데이터 JSON 에 prefetch 인라인 카탈로그를 끼워 넣은 새 JSON 을 반환합니다.
        ///
        /// <paramref name="metadataJson"/> 는 <see cref="BuildMetadataJson"/> 가 만든
        /// <c>{...}</c> 형태의 compact JSON 이어야 하며, 마지막 닫는 중괄호 직전에
        /// <c>,"prefetch":&lt;catalog&gt;</c> 를 삽입합니다.
        ///
        /// <paramref name="compactCatalogJson"/> 가 비어 있거나 metadataJson 이 <c>}</c> 로
        /// 끝나지 않으면 원본을 그대로 반환합니다(안전 폴백 — 기존 출력과 byte-identical).
        /// </summary>
        /// <param name="metadataJson">BuildMetadataJson 산출물(single-line JSON).</param>
        /// <param name="compactCatalogJson">AITWarmManifestEmitter 가 반환한 compact v2 카탈로그. null/빈 문자열이면 무시.</param>
        internal static string WithPrefetch(string metadataJson, string compactCatalogJson)
        {
            if (string.IsNullOrEmpty(metadataJson)) return metadataJson;
            if (string.IsNullOrEmpty(compactCatalogJson)) return metadataJson;

            string trimmed = metadataJson.TrimEnd();
            if (!trimmed.EndsWith("}", StringComparison.Ordinal)) return metadataJson;

            // 마지막 '}' 직전에 prefetch 키를 삽입.
            int lastBrace = trimmed.LastIndexOf('}');
            string head = trimmed.Substring(0, lastBrace);
            // head 가 "{...필드" 면 ',' 로, "{"(빈 객체)면 콤마 없이 이어 붙임.
            bool hasFields = head.TrimStart().Length > 1; // "{" 보다 길면 필드가 존재.
            string separator = hasFields ? "," : "";
            return head + separator + "\"prefetch\":" + compactCatalogJson + "}";
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
        /// prefetch 카탈로그는 WebGL 복사 이후 <see cref="WithPrefetch"/> 로 별도 패치됩니다.
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
