// -----------------------------------------------------------------------
// <copyright file="AITSentryDsnInjector.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Sentry DSN Build-time Injector
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Sentry.Unity;

namespace AppsInToss.Sentry.Editor
{
    /// <summary>
    /// WebGL 빌드 시 SENTRY_DSN 환경변수에서 SentryOptions.asset을 자동 생성합니다.
    ///
    /// WebGL(브라우저 샌드박스)에서는 런타임에 환경변수를 읽을 수 없으므로,
    /// 빌드 시점에 환경변수를 asset으로 bake해야 합니다.
    ///
    /// callbackOrder=0으로 Sentry 자체 빌드 프로세서(order=1)보다 먼저 실행됩니다.
    /// </summary>
    internal class AITSentryDsnInjector : IPreprocessBuildWithReport
    {
        private const string Tag = "[AITSentry]";

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.WebGL)
                return;

            InjectSentryDsnFromEnvironment();
        }

        private static void InjectSentryDsnFromEnvironment()
        {
            string dsn = Environment.GetEnvironmentVariable("SENTRY_DSN");
            if (string.IsNullOrEmpty(dsn))
            {
                Debug.Log($"{Tag} SENTRY_DSN 환경변수가 설정되지 않았습니다 - Sentry DSN 자동 주입을 건너뜁니다");
                return;
            }

            string configPath = ScriptableSentryUnityOptions.GetConfigPath();

            if (File.Exists(Path.Combine(Application.dataPath, "..", configPath)))
            {
                Debug.Log($"{Tag} SentryOptions.asset이 이미 존재합니다 - 사용자 설정을 유지합니다: {configPath}");
                return;
            }

            // asset 디렉토리 생성
            string assetDir = Path.GetDirectoryName(configPath);
            string fullDir = Path.Combine(Application.dataPath, "..", assetDir);
            if (!Directory.Exists(fullDir))
            {
                Directory.CreateDirectory(fullDir);
            }

            // Unity API로 ScriptableSentryUnityOptions 생성
            var options = ScriptableObject.CreateInstance<ScriptableSentryUnityOptions>();
            options.Enabled = true;
            options.Dsn = dsn;

            string environment = Environment.GetEnvironmentVariable("SENTRY_ENVIRONMENT");
            if (!string.IsNullOrEmpty(environment))
            {
                options.EnvironmentOverride = environment;
            }

            string release = Environment.GetEnvironmentVariable("SENTRY_RELEASE");
            if (!string.IsNullOrEmpty(release))
            {
                options.ReleaseOverride = release;
            }

            AssetDatabase.CreateAsset(options, configPath);
            AssetDatabase.SaveAssets();

            Debug.Log($"{Tag} SentryOptions.asset을 환경변수에서 생성했습니다: {configPath}");
            Debug.Log($"{Tag}   DSN: {MaskDsn(dsn)}");
            if (!string.IsNullOrEmpty(environment))
                Debug.Log($"{Tag}   Environment: {environment}");
            if (!string.IsNullOrEmpty(release))
                Debug.Log($"{Tag}   Release: {release}");
        }

        /// <summary>
        /// DSN의 키 부분을 마스킹합니다. (예: https://***@o0.ingest.sentry.io/123)
        /// </summary>
        private static string MaskDsn(string dsn)
        {
            // DSN 형식: https://{key}@{host}/{projectId}
            int atIndex = dsn.IndexOf('@');
            int schemeEnd = dsn.IndexOf("://");
            if (atIndex > 0 && schemeEnd > 0 && schemeEnd < atIndex)
            {
                return dsn.Substring(0, schemeEnd + 3) + "***" + dsn.Substring(atIndex);
            }
            return "***";
        }
    }
}
