// -----------------------------------------------------------------------
// <copyright file="AITSentryContextEnricher.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Sentry Context Enricher
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using Sentry;
using Sentry.Unity;
using UnityEngine;
using UnityEngine.Scripting;

// Sentry Unity SDK 4.0+: SentrySdk는 Sentry.Unity 네임스페이스에 위치
using SentrySdk = Sentry.Unity.SentrySdk;

namespace AppsInToss.Sentry
{
    /// <summary>
    /// AIT 플랫폼 API를 비동기 호출하여 Sentry 스코프에 컨텍스트를 적용합니다.
    /// </summary>
    [Preserve]
    internal static class AITSentryContextEnricher
    {
        private const string Tag = "[AITSentry]";
        private const string Unavailable = "unavailable";

        /// <summary>
        /// AIT 플랫폼 컨텍스트를 비동기로 수집하여 Sentry 스코프에 적용합니다.
        /// fire-and-forget 패턴으로 호출됩니다.
        /// </summary>
        internal static async void EnrichAsync()
        {
            try
            {
                var context = new Dictionary<string, string>();

                context["sdk_version"] = AITVersion.FullVersion;
                context["unity_version"] = Application.unityVersion;

                var deviceId = await CollectSafe("GetDeviceId", CallGetDeviceId);
                var platformOS = await CollectSafe("GetPlatformOS", CallGetPlatformOS);
                var locale = await CollectSafe("GetLocale", CallGetLocale);
                var tossAppVersion = await CollectSafe("GetTossAppVersion", CallGetTossAppVersion);
                var environment = await CollectSafe("GetOperationalEnvironment", CallGetOperationalEnvironment);
                var deploymentId = await CollectSafe("EnvGetDeploymentId", CallEnvGetDeploymentId);

                context["device_id"] = deviceId;
                context["platform_os"] = platformOS;
                context["locale"] = locale;
                context["toss_app_version"] = tossAppVersion;
                context["environment"] = environment;
                context["deployment_id"] = deploymentId;

                SentrySdk.ConfigureScope(scope =>
                {
                    if (!string.IsNullOrEmpty(deviceId) && deviceId != Unavailable)
                    {
                        scope.SetTag("ait.device_id", deviceId);
                        scope.User ??= new SentryUser();
                        scope.User.Id = deviceId;
                    }

                    SetTagIfAvailable(scope, "ait.platform_os", platformOS);
                    SetTagIfAvailable(scope, "ait.locale", locale);
                    SetTagIfAvailable(scope, "ait.toss_app_version", tossAppVersion);
                    SetTagIfAvailable(scope, "ait.environment", environment);
                    SetTagIfAvailable(scope, "ait.deployment_id", deploymentId);

                    scope.Contexts["apps_in_toss"] = context;
                });

                Debug.Log($"{Tag} AIT 컨텍스트 수집 완료 (device={deviceId}, platform={platformOS}, locale={locale})");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{Tag} 컨텍스트 수집 중 예기치 않은 오류: {ex.Message}");
            }
        }

        private static void SetTagIfAvailable(Scope scope, string key, string value)
        {
            if (!string.IsNullOrEmpty(value) && value != Unavailable)
            {
                scope.SetTag(key, value);
            }
        }

#if UNITY_6000_0_OR_NEWER
        private delegate Awaitable<string> AsyncStringCall();

        private static async Awaitable<string> CollectSafe(string apiName, AsyncStringCall call)
        {
            try
            {
                return await call();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{Tag} {apiName} 호출 실패: {ex.Message}");
                return Unavailable;
            }
        }

        private static Awaitable<string> CallGetDeviceId() => AIT.GetDeviceId();
        private static Awaitable<string> CallGetPlatformOS() => AIT.GetPlatformOS();
        private static Awaitable<string> CallGetLocale() => AIT.GetLocale();
        private static Awaitable<string> CallGetTossAppVersion() => AIT.GetTossAppVersion();
        private static Awaitable<string> CallGetOperationalEnvironment() => AIT.GetOperationalEnvironment();
        private static Awaitable<string> CallEnvGetDeploymentId() => AIT.EnvGetDeploymentId();
#else
        private delegate System.Threading.Tasks.Task<string> AsyncStringCall();

        private static async System.Threading.Tasks.Task<string> CollectSafe(string apiName, AsyncStringCall call)
        {
            try
            {
                return await call();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{Tag} {apiName} 호출 실패: {ex.Message}");
                return Unavailable;
            }
        }

        private static System.Threading.Tasks.Task<string> CallGetDeviceId() => AIT.GetDeviceId();
        private static System.Threading.Tasks.Task<string> CallGetPlatformOS() => AIT.GetPlatformOS();
        private static System.Threading.Tasks.Task<string> CallGetLocale() => AIT.GetLocale();
        private static System.Threading.Tasks.Task<string> CallGetTossAppVersion() => AIT.GetTossAppVersion();
        private static System.Threading.Tasks.Task<string> CallGetOperationalEnvironment() => AIT.GetOperationalEnvironment();
        private static System.Threading.Tasks.Task<string> CallEnvGetDeploymentId() => AIT.EnvGetDeploymentId();
#endif
    }
}
