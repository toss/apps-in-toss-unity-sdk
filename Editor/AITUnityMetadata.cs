using System;
using System.Collections.Generic;
using AppsInToss;
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
        /// Unity 빌드 메타데이터를 JSON 문자열로 반환합니다.
        /// </summary>
        internal static string BuildMetadataJson()
        {
            var metadata = new Dictionary<string, object>
            {
                { "unityVersion", Application.unityVersion },
                { "bundleVersion", PlayerSettings.bundleVersion },
                { "sdkVersion", AITVersion.Version },
                { "sdkCommitHash", AITVersion.CommitHash ?? "" },
                { "productName", PlayerSettings.productName },
                { "companyName", PlayerSettings.companyName },
                { "buildTimestamp", DateTime.UtcNow.ToString("o") },
            };
            return MiniJson.Serialize(metadata);
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
