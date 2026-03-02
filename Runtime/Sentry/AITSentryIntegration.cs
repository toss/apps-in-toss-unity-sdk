// -----------------------------------------------------------------------
// <copyright file="AITSentryIntegration.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Sentry Integration
// </copyright>
// -----------------------------------------------------------------------

using System;
using Sentry;
using Sentry.Unity;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Scripting;

// Sentry Unity SDK 4.0+: SentrySdk는 Sentry.Unity 네임스페이스에 위치
// Scope, BreadcrumbLevel 등 기타 타입은 Sentry 네임스페이스에 위치
using SentrySdk = Sentry.Unity.SentrySdk;

// IL2CPP 링커가 이 어셈블리를 참조 없이도 항상 처리하도록 보장합니다.
// AppsInToss.Sentry는 [RuntimeInitializeOnLoadMethod]로만 진입하며,
// 다른 어셈블리에서 직접 참조하지 않으므로 링커가 제거할 수 있습니다.
[assembly: AlwaysLinkAssembly]

namespace AppsInToss.Sentry
{
    /// <summary>
    /// Sentry SDK가 설치된 경우 AIT 플랫폼 컨텍스트를 자동으로 Sentry 이벤트에 추가하는 통합 모듈
    /// </summary>
    /// <remarks>
    /// RuntimeInitializeOnLoadMethod(AfterSceneLoad)로 자동 초기화됩니다.
    /// Sentry SDK와 AIT SDK 모두 BeforeSceneLoad에서 초기화되므로, AfterSceneLoad에서 안전하게 접근합니다.
    /// </remarks>
    [Preserve]
    internal static class AITSentryIntegration
    {
        private const string Tag = "[AITSentry]";
        private static bool _initialized;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            if (!SentrySdk.IsEnabled)
            {
                Debug.Log($"{Tag} Sentry SDK가 비활성 상태입니다. AIT 컨텍스트 연동을 건너뜁니다. (DSN 설정 확인: Tools > Sentry)");
                return;
            }

            SetStaticTags();
            SubscribeSceneEvents();
            AITSentryContextEnricher.EnrichAsync();
            AITSentryAnalytics.Initialize();

            Debug.Log($"{Tag} Initialized - AIT 컨텍스트가 Sentry 이벤트에 자동으로 추가됩니다.");
        }

        private static void SetStaticTags()
        {
            SentrySdk.ConfigureScope(scope =>
            {
                scope.SetTag("ait.sdk_version", AITVersion.FullVersion);
                scope.SetTag("ait.unity_version", Application.unityVersion);
            });
        }

        private static void SubscribeSceneEvents()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;

            var activeScene = SceneManager.GetActiveScene();
            if (!string.IsNullOrEmpty(activeScene.name))
            {
                SentrySdk.ConfigureScope(scope =>
                {
                    scope.SetTag("ait.current_scene", activeScene.name);
                });
            }
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!SentrySdk.IsEnabled) return;

            try
            {
                SentrySdk.AddBreadcrumb(
                    message: $"Scene loaded: {scene.name}",
                    category: "scene",
                    level: BreadcrumbLevel.Info,
                    data: new System.Collections.Generic.Dictionary<string, string>
                    {
                        { "scene_name", scene.name },
                        { "scene_build_index", scene.buildIndex.ToString() },
                        { "load_mode", mode.ToString() }
                    }
                );

                SentrySdk.ConfigureScope(scope =>
                {
                    scope.SetTag("ait.current_scene", scene.name);
                });
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{Tag} Scene breadcrumb 기록 실패: {ex.Message}");
            }
        }
    }
}
