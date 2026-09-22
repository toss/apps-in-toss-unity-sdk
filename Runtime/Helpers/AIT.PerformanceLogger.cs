// -----------------------------------------------------------------------
// <copyright file="AIT.PerformanceLogger.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Runtime Event Logger
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Scripting;

namespace AppsInToss
{
    /// <summary>
    /// Unity 런타임 이벤트를 자동으로 수집하여 플랫폼에 전송하는 로거
    /// </summary>
    /// <remarks>
    /// RuntimeInitializeOnLoadMethod로 자동 초기화되며, 사용자 코드 작성이 불필요합니다.
    /// 수집 이벤트: Scene 전환, Low Memory, 에러/예외, 앱 라이프사이클,
    /// 프레임 스톨, 화면 변경, GC 수집, TimeScale 변경, first-interactive
    /// (first-interactive: 원래 첫 씬 로드 완료 시점 — time-to-original-scene.
    ///  부팅 첫 씬 로드는 first-paint 직전에 발생하므로 base 빌드에서는 두 지표가 근접함)
    /// </remarks>
    [Preserve]
    internal static class AITPerformanceLogger
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __AITDebugLog_Send(string jsonStr);

        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern int __AITDebugLog_FirstInteractiveEnabled();
#endif

        private const string Tag = "[AITPerformanceLogger]";

        // first-interactive 관련 상수 및 상태
        private const string ProxyBootScenePrefix = "AITProxyBoot";
        private static bool _firstInteractiveSent;
        private static int _firstInteractiveEnabledCache = -1; // -1=미확인, 0=비활성, 1=활성

        // Rate limit constants
        private const float LowMemoryIntervalSec = 30f;
        private const float ErrorIntervalSec = 60f;
        private const int ErrorMaxPerInterval = 10;
        private const float FocusIntervalSec = 5f;
        private const float FrameStallIntervalSec = 60f;
        private const int FrameStallMaxPerInterval = 5;
        private const float ScreenChangeIntervalSec = 2f;
        private const float GCCollectionIntervalSec = 60f;
        private const int GCCollectionMaxPerInterval = 5;
        private const float TimeScaleIntervalSec = 5f;

        // Frame stall threshold
        private const float FrameStallThresholdSec = 0.5f;

        // Error message limits
        private const int ErrorMessageMaxLength = 500;
        private const int ErrorStackTraceMaxLength = 200;

        // Rate limit state
        private static float _lastLowMemoryTime = float.NegativeInfinity;
        private static float _lastFocusTime = float.NegativeInfinity;
        private static float _lastScreenChangeTime = float.NegativeInfinity;
        private static float _lastTimeScaleChangeTime = float.NegativeInfinity;

        private static float _errorWindowStart;
        private static int _errorCountInWindow;
        private static readonly HashSet<int> _recentErrorHashes = new HashSet<int>();

        private static float _frameStallWindowStart;
        private static int _frameStallCountInWindow;

        private static float _gcWindowStart;
        private static int _gcCountInWindow;

        // Re-entrancy guard
        private static bool _isSending;

        // Scene tracking
        private static string _lastSceneName = "";
        private static int _totalScenesLoaded;

        // Initialization flag
        private static bool _initialized;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            // Track initial scene
            var activeScene = SceneManager.GetActiveScene();
            _lastSceneName = activeScene.name;

            // Subscribe to events
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            Application.lowMemory += OnLowMemory;
            Application.logMessageReceived += OnLogMessage;
            // jslib lazy init 강제 트리거 + VisibilityHelper 구독
            _ = AITVisibilityHelper.IsVisible;
            AITVisibilityHelper.OnVisibilityChanged += OnVisibilityChanged;
            Application.quitting += OnQuitting;

            // Create polling MonoBehaviour
            var go = new GameObject("AITPerformanceLoggerMonitor");
            go.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<AITPerformanceLoggerMonitor>();

            Debug.Log($"{Tag} Initialized");
        }

        private static void SendLog(string logName, Dictionary<string, object> parameters)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            // jslib bridge는 WebGL 빌드에서만 사용 가능하므로 그 외 환경에서는 무시
            // Debug.LogWarning 사용 불가: logMessageReceived → SendLog 재귀 호출 위험
            return;
#else
            if (_isSending) return;
            _isSending = true;

            try
            {
                var eventLogParams = new EventLogParams
                {
                    Log_name = logName,
                    Log_type = "unity_runtime",
                    Params = parameters
                };
                __AITDebugLog_Send(AITJsonSettings.Serialize(eventLogParams));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{Tag} Failed to send log '{logName}': {ex.Message}");
            }
            finally
            {
                _isSending = false;
            }
#endif
        }

        // ---- 1. Scene Transition ----
        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            try
            {
                _totalScenesLoaded++;
                var previousScene = _lastSceneName;
                _lastSceneName = scene.name;

                SendLog("unity_scene_transition", new Dictionary<string, object>
                {
                    { "event_type", "scene_loaded" },
                    { "scene_name", scene.name },
                    { "scene_build_index", scene.buildIndex },
                    { "load_mode", mode.ToString() },
                    { "previous_scene", previousScene },
                    { "total_loaded_scenes", SceneManager.sceneCount },
                    { "time_since_start_sec", Math.Round(Time.realtimeSinceStartup, 1) }
                });

                // first-interactive: 원래 첫 씬 로드 완료 시점 계측 (once-per-session).
                // 활성 여부는 빌드 시 템플릿에 새겨진 상수(부재 시 fail-open=활성)라 세션 중 변하지 않으며,
                // 최초 대상 씬에서 활성 여부와 무관하게 _firstInteractiveSent를 고정한다 — 이후 씬은 "first"가 아니다.
                if (ShouldEmitFirstInteractive(scene.name, _firstInteractiveSent))
                {
                    bool enabled = IsFirstInteractiveLogEnabled();
                    _firstInteractiveSent = true; // 활성 여부와 무관하게 "최초" 의미 고정 (이후 씬은 first가 아님)
                    if (enabled)
                    {
                        SendLog("unity_first_interactive", new Dictionary<string, object>
                        {
                            { "event_type", "first_interactive" },
                            { "scene_name", scene.name },
                            { "scene_build_index", scene.buildIndex },
                            { "time_since_start_ms", (long)Math.Round(Time.realtimeSinceStartup * 1000.0, 0) }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{Tag} OnSceneLoaded error: {ex.Message}");
            }
        }

        /// <summary>
        /// first-interactive 이벤트를 발화해야 하는지 판단한다.
        /// </summary>
        /// <param name="sceneName">로드된 씬 이름</param>
        /// <param name="alreadySent">이미 발화된 경우 true</param>
        /// <returns>
        /// alreadySent=true 이면 false(중복 방지).
        /// sceneName이 null/empty 이면 true(이름 없는 씬도 최초 실 씬으로 취급).
        /// sceneName이 "AITProxyBoot"로 시작하면 false(SDK 주입 프록시 부팅 씬은 원래 첫 씬이 아님).
        /// 그 외는 true.
        /// </returns>
        internal static bool ShouldEmitFirstInteractive(string sceneName, bool alreadySent)
        {
            if (alreadySent) return false;
            if (string.IsNullOrEmpty(sceneName)) return true;
            if (sceneName.StartsWith(ProxyBootScenePrefix, StringComparison.Ordinal)) return false;
            return true;
        }

        /// <summary>
        /// first-interactive 로그 활성 여부를 반환한다.
        /// WebGL 비에디터 환경에서 jslib extern을 1회 호출한 뒤 캐시한다(fail-open).
        /// 그 외 환경에서는 항상 false(SendLog가 어차피 no-op).
        /// </summary>
        private static bool IsFirstInteractiveLogEnabled()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (_firstInteractiveEnabledCache < 0)
            {
                try
                {
                    _firstInteractiveEnabledCache = __AITDebugLog_FirstInteractiveEnabled();
                }
                catch
                {
                    _firstInteractiveEnabledCache = 1; // fail-open
                }
            }
            return _firstInteractiveEnabledCache == 1;
#else
            return false;
#endif
        }

        private static void OnSceneUnloaded(Scene scene)
        {
            try
            {
                SendLog("unity_scene_transition", new Dictionary<string, object>
                {
                    { "event_type", "scene_unloaded" },
                    { "scene_name", scene.name },
                    { "scene_build_index", scene.buildIndex },
                    { "total_loaded_scenes", SceneManager.sceneCount },
                    { "time_since_start_sec", Math.Round(Time.realtimeSinceStartup, 1) }
                });
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{Tag} OnSceneUnloaded error: {ex.Message}");
            }
        }

        // ---- 2. Low Memory ----
        private static void OnLowMemory()
        {
            try
            {
                float now = Time.realtimeSinceStartup;
                if (now - _lastLowMemoryTime < LowMemoryIntervalSec) return;
                _lastLowMemoryTime = now;

                SendLog("unity_low_memory", new Dictionary<string, object>
                {
                    { "event_type", "low_memory" },
                    { "time_since_start_sec", Math.Round(now, 1) }
                });
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{Tag} OnLowMemory error: {ex.Message}");
            }
        }

        // ---- 3. Error/Exception ----
        private static void OnLogMessage(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)
                return;

            try
            {
                float now = Time.realtimeSinceStartup;

                // Reset window if expired
                if (now - _errorWindowStart >= ErrorIntervalSec)
                {
                    _errorWindowStart = now;
                    _errorCountInWindow = 0;
                    _recentErrorHashes.Clear();
                }

                // Rate limit check
                if (_errorCountInWindow >= ErrorMaxPerInterval) return;

                // Deduplicate by message hash
                int hash = (condition ?? "").GetHashCode();
                if (!_recentErrorHashes.Add(hash)) return;

                _errorCountInWindow++;

                string eventType;
                switch (type)
                {
                    case LogType.Exception: eventType = "exception"; break;
                    case LogType.Assert: eventType = "assert"; break;
                    default: eventType = "error"; break;
                }

                SendLog("unity_error", new Dictionary<string, object>
                {
                    { "event_type", eventType },
                    { "message", Truncate(condition, ErrorMessageMaxLength) },
                    { "stack_trace", Truncate(stackTrace, ErrorStackTraceMaxLength) },
                    { "log_type", type.ToString() },
                    { "time_since_start_sec", Math.Round(now, 1) }
                });
            }
            catch (Exception)
            {
                // Silently ignore to prevent recursive logging
            }
        }

        // ---- 4. App Lifecycle ----
        private static void OnVisibilityChanged(bool isVisible)
        {
            try
            {
                float now = Time.realtimeSinceStartup;
                if (now - _lastFocusTime < FocusIntervalSec) return;
                _lastFocusTime = now;

                SendLog("unity_lifecycle", new Dictionary<string, object>
                {
                    { "event_type", "focus_changed" },
                    { "has_focus", isVisible },
                    { "time_since_start_sec", Math.Round(now, 1) }
                });
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{Tag} OnVisibilityChanged error: {ex.Message}");
            }
        }

        private static void OnQuitting()
        {
            try
            {
                float now = Time.realtimeSinceStartup;
                SendLog("unity_lifecycle", new Dictionary<string, object>
                {
                    { "event_type", "quitting" },
                    { "session_duration_sec", Math.Round(now, 1) },
                    { "total_scenes_loaded", _totalScenesLoaded },
                });
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{Tag} OnQuitting error: {ex.Message}");
            }
        }

        // ---- Polling events (called from MonoBehaviour.Update) ----

        internal static void OnUpdate()
        {
            float now = Time.realtimeSinceStartup;

            CheckFrameStall(now);
            CheckScreenChange(now);
            CheckGCCollection(now);
            CheckTimeScaleChange(now);
        }

        // ---- 5. Frame Stall ----
        private static void CheckFrameStall(float now)
        {
            try
            {
                float delta = Time.unscaledDeltaTime;
                if (delta <= FrameStallThresholdSec) return;

                // Reset window if expired
                if (now - _frameStallWindowStart >= FrameStallIntervalSec)
                {
                    _frameStallWindowStart = now;
                    _frameStallCountInWindow = 0;
                }

                if (_frameStallCountInWindow >= FrameStallMaxPerInterval) return;
                _frameStallCountInWindow++;

                SendLog("unity_frame_stall", new Dictionary<string, object>
                {
                    { "event_type", "frame_stall" },
                    { "frame_duration_ms", Math.Round(delta * 1000.0, 0) },
                    { "threshold_ms", (int)(FrameStallThresholdSec * 1000) },
                    { "time_since_start_sec", Math.Round(now, 1) }
                });
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{Tag} CheckFrameStall error: {ex.Message}");
            }
        }

        // ---- 6. Screen Change ----
        private static int _lastScreenWidth;
        private static int _lastScreenHeight;
        private static ScreenOrientation _lastOrientation;
        private static bool _screenInitialized;

        private static void CheckScreenChange(float now)
        {
            try
            {
                int w = Screen.width;
                int h = Screen.height;
                ScreenOrientation orientation = Screen.orientation;

                if (!_screenInitialized)
                {
                    _lastScreenWidth = w;
                    _lastScreenHeight = h;
                    _lastOrientation = orientation;
                    _screenInitialized = true;
                    return;
                }

                if (w == _lastScreenWidth && h == _lastScreenHeight && orientation == _lastOrientation)
                    return;

                if (now - _lastScreenChangeTime < ScreenChangeIntervalSec) return;
                _lastScreenChangeTime = now;

                bool orientationChanged = orientation != _lastOrientation;
                bool sizeChanged = w != _lastScreenWidth || h != _lastScreenHeight;

                if (orientationChanged)
                {
                    SendLog("unity_screen_change", new Dictionary<string, object>
                    {
                        { "event_type", "orientation_change" },
                        { "width", w },
                        { "height", h },
                        { "orientation", orientation.ToString() },
                        { "previous_orientation", _lastOrientation.ToString() },
                        { "time_since_start_sec", Math.Round(now, 1) }
                    });
                }
                else if (sizeChanged)
                {
                    SendLog("unity_screen_change", new Dictionary<string, object>
                    {
                        { "event_type", "screen_resize" },
                        { "width", w },
                        { "height", h },
                        { "previous_width", _lastScreenWidth },
                        { "previous_height", _lastScreenHeight },
                        { "time_since_start_sec", Math.Round(now, 1) }
                    });
                }

                _lastScreenWidth = w;
                _lastScreenHeight = h;
                _lastOrientation = orientation;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{Tag} CheckScreenChange error: {ex.Message}");
            }
        }

        // ---- 7. GC Collection ----
        private static int _lastGCCount;
        private static bool _gcInitialized;

        private static void CheckGCCollection(float now)
        {
            try
            {
                int gen0Count = GC.CollectionCount(0);

                if (!_gcInitialized)
                {
                    _lastGCCount = gen0Count;
                    _gcInitialized = true;
                    return;
                }

                if (gen0Count == _lastGCCount) return;

                // Reset window if expired
                if (now - _gcWindowStart >= GCCollectionIntervalSec)
                {
                    _gcWindowStart = now;
                    _gcCountInWindow = 0;
                }

                if (_gcCountInWindow >= GCCollectionMaxPerInterval) return;
                _gcCountInWindow++;

                // Determine which generation was collected
                int gen1Count = GC.CollectionCount(1);
                int gen2Count = GC.CollectionCount(2);
                int generation = 0;
                if (gen2Count > 0 && gen1Count > 0) generation = 2;
                else if (gen1Count > 0) generation = 1;

                SendLog("unity_gc_collection", new Dictionary<string, object>
                {
                    { "event_type", "gc_collection" },
                    { "generation", generation },
                    { "gen0_total", gen0Count },
                    { "gen1_total", gen1Count },
                    { "gen2_total", gen2Count },
                    { "time_since_start_sec", Math.Round(now, 1) }
                });

                _lastGCCount = gen0Count;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{Tag} CheckGCCollection error: {ex.Message}");
            }
        }

        // ---- 8. TimeScale Change ----
        private static float _lastTimeScale = 1f;
        private static bool _timeScaleInitialized;

        private static void CheckTimeScaleChange(float now)
        {
            try
            {
                float timeScale = Time.timeScale;

                if (!_timeScaleInitialized)
                {
                    _lastTimeScale = timeScale;
                    _timeScaleInitialized = true;
                    return;
                }

                // ReSharper disable once CompareOfFloatsByEqualityOperator
                if (timeScale == _lastTimeScale) return;

                if (now - _lastTimeScaleChangeTime < TimeScaleIntervalSec) return;
                _lastTimeScaleChangeTime = now;

                float previousTimeScale = _lastTimeScale;
                _lastTimeScale = timeScale;

                SendLog("unity_timescale_change", new Dictionary<string, object>
                {
                    { "event_type", "timescale_changed" },
                    { "time_scale", Math.Round(timeScale, 3) },
                    { "previous_time_scale", Math.Round(previousTimeScale, 3) },
                    { "time_since_start_sec", Math.Round(now, 1) }
                });
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{Tag} CheckTimeScaleChange error: {ex.Message}");
            }
        }

        // ---- Utility ----
        private static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value.Length <= maxLength ? value : value.Substring(0, maxLength);
        }
    }

    /// <summary>
    /// Update() 루프에서 폴링 기반 이벤트를 감지하는 내부 MonoBehaviour
    /// </summary>
    [Preserve]
    internal class AITPerformanceLoggerMonitor : MonoBehaviour
    {
        private void Update()
        {
            AITPerformanceLogger.OnUpdate();
        }
    }
}
