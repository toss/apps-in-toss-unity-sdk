// -----------------------------------------------------------------------
// <copyright file="AITSentryDeferredInit.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Sentry Deferred Initialization
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using Sentry;
using Sentry.Unity;
using UnityEngine;
using UnityEngine.Scripting;

using SentrySdk = Sentry.Unity.SentrySdk;

namespace AppsInToss.Sentry
{
    /// <summary>
    /// Sentry 초기화를 첫 프레임 뒤로 미룬다.
    /// </summary>
    /// <remarks>
    /// Sentry Unity는 WebGL에서 BeforeSceneLoad에 <c>SentrySdk.Init</c>을 부르는데, 첫 프레임 전에
    /// 4x CPU 기준 ~200ms를 쓴다(대부분 AutoSessionTracking의 SentrySession 생성자가 처음 부르는
    /// DateTime.Now의 TimeZoneInfo 초기화). 그보다 앞선 AfterAssembliesLoaded에서 옵션 asset의
    /// Enabled를 메모리에서만 끄면 Sentry 자체 초기화는 건너뛰고, 두 번째 프레임에서 되돌려 같은
    /// 옵션으로 초기화한다. 그 사이 Exception/Error 로그는 모아 뒀다가 초기화 직후 전송한다.
    /// 끄려면 Scripting Define Symbols에 AIT_SENTRY_EAGER_INIT을 추가한다.
    /// </remarks>
    [Preserve]
    internal static class AITSentryDeferredInit
    {
        private const string Tag = "[AITSentry]";
        // ScriptableSentryUnityOptions.ConfigRootFolder/ConfigName(internal)과 같은 Resources 경로
        private const string OptionsResourcePath = "Sentry/SentryOptions";
        private const int MaxBufferedLogs = 20;
        private const int FramesToWait = 2;

        private static ScriptableSentryUnityOptions _scriptableOptions;
        private static List<BufferedLog> _buffer;
        private static Action _onInitialized;

        /// <summary>지연 초기화가 아직 끝나지 않았는지.</summary>
        internal static bool Pending { get; private set; }

        /// <summary>
        /// 지연 초기화가 끝난 뒤 실행한다. 지연 중이 아니면 바로 실행한다.
        /// </summary>
        internal static void RunAfterInit(Action action)
        {
            if (Pending)
            {
                _onInitialized += action;
            }
            else
            {
                action();
            }
        }

        // 에디터 Play 모드에서는 끈다. 거기서 asset의 Enabled를 바꾸면 디스크에 저장될 수 있다.
#if UNITY_WEBGL && !UNITY_EDITOR && !AIT_SENTRY_EAGER_INIT
        private static readonly bool DeferSupported = true;
#else
        private static readonly bool DeferSupported = false;
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void Suspend()
        {
            if (!DeferSupported) return;

            try
            {
                _scriptableOptions = Resources.Load<ScriptableSentryUnityOptions>(OptionsResourcePath);
                if (_scriptableOptions == null || !_scriptableOptions.Enabled || string.IsNullOrWhiteSpace(_scriptableOptions.Dsn))
                {
                    return;
                }

                _scriptableOptions.Enabled = false;
                Pending = true;
                _buffer = new List<BufferedLog>();
                Application.logMessageReceived += BufferLog;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{Tag} Sentry 지연 초기화 준비 실패, 기본 초기화로 진행합니다: {ex.Message}");
                Restore();
            }
        }

        // 씬 로드 뒤에 만들어야 첫 프레임 Update부터 센다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartDriver()
        {
            if (!Pending) return;

            var driver = new GameObject("AITSentryDeferredInit") { hideFlags = HideFlags.HideInHierarchy };
            UnityEngine.Object.DontDestroyOnLoad(driver);
            driver.AddComponent<Driver>();
        }

        private static void Restore()
        {
            if (_scriptableOptions != null)
            {
                _scriptableOptions.Enabled = true;
            }
            Application.logMessageReceived -= BufferLog;
            Pending = false;
        }

        private static void Resume()
        {
            if (!Pending) return;

            var buffered = _buffer;
            _buffer = null;
            Restore();

            try
            {
                var options = ScriptableSentryUnityOptions.LoadSentryUnityOptions();
                if (options != null && options.ShouldInitializeSdk())
                {
                    SentrySdk.Init(options);
                    Replay(buffered, options.CaptureLogErrorEvents);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{Tag} Sentry 지연 초기화 실패: {ex.Message}");
            }

            var callbacks = _onInitialized;
            _onInitialized = null;
            callbacks?.Invoke();
        }

        private static void BufferLog(string condition, string stackTrace, LogType type)
        {
            if (_buffer == null || _buffer.Count >= MaxBufferedLogs) return;
            if (type != LogType.Exception && type != LogType.Error && type != LogType.Assert) return;
            _buffer.Add(new BufferedLog(condition, stackTrace, type));
        }

        private static void Replay(List<BufferedLog> buffered, bool captureLogErrors)
        {
            if (buffered == null || !SentrySdk.IsEnabled) return;

            foreach (var log in buffered)
            {
                if (log.Type != LogType.Exception && !captureLogErrors) continue;

                SentrySdk.CaptureMessage(log.Condition, scope =>
                {
                    scope.SetTag("ait.sentry_deferred", "replayed");
                    scope.SetTag("unity.log_type", log.Type.ToString());
                    scope.SetExtra("unity.stacktrace", log.StackTrace);
                }, SentryLevel.Error);
            }
        }

        private readonly struct BufferedLog
        {
            public readonly string Condition;
            public readonly string StackTrace;
            public readonly LogType Type;

            public BufferedLog(string condition, string stackTrace, LogType type)
            {
                Condition = condition;
                StackTrace = stackTrace;
                Type = type;
            }
        }

        [AddComponentMenu("")]
        private sealed class Driver : MonoBehaviour
        {
            private int _frames;

            private void Update()
            {
                if (++_frames < FramesToWait) return;
                Resume();
                Destroy(gameObject);
            }
        }
    }
}
