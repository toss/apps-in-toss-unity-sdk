// -----------------------------------------------------------------------
// <copyright file="AIT.HookTimer.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Boot Hook Timer
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

// AITSentryIntegration(AppsInToss.Sentry 어셈블리)이 이 어셈블리(AppsInToss.Helpers)의
// internal 계측 API(AITHookTimer)를 그대로 쓸 수 있도록 노출한다.
// AppsInToss.Sentry.asmdef 는 이미 AppsInToss.Helpers 를 참조하지만, C# internal 은
// 어셈블리 참조만으로는 노출되지 않으므로 InternalsVisibleTo 가 필요하다.
[assembly: InternalsVisibleTo("AppsInToss.Sentry")]

namespace AppsInToss
{
    /// <summary>
    /// SDK가 등록하는 RuntimeInitializeOnLoadMethod 훅(Sentry, StreamingFont, VisibilityHelper,
    /// StreamingAudio, StreamingTexture, AITVersion, PerformanceLogger)의 개별 실행 시간을 계측한다.
    /// </summary>
    /// <remarks>
    /// 게이팅: AIT_FIRST_INTERACTIVE_LOG와 동일한 게이트(<see cref="AITPerformanceLogger.IsFirstInteractiveLogEnabled"/>)를
    /// 그대로 재사용한다. 게이트가 꺼져 있으면 <see cref="Begin"/>이 캐시된 bool 하나만 확인하고
    /// 즉시 no-op Scope(default)를 반환하므로, 시각 측정/JS interop/문자열 작업이 전혀 발생하지 않는다.
    ///
    /// 게이트가 켜진 경우:
    ///  - 각 훅의 소요 시간을 (힙 할당 없는) Stopwatch 타임스탬프로 측정해, AITPerformanceLogger가
    ///    "unity_first_interactive" 이벤트에 함께 실어 보내는 숫자 집계에 반영한다.
    ///  - WebGL(비에디터) 빌드에서는 performance.mark/performance.measure도 함께 남겨
    ///    devtools Performance 타임라인에서 훅별 구간을 그대로 확인할 수 있게 한다.
    ///  - 에디터 플레이모드 등 performance.*가 없는 환경에서는 JS mark 호출만 no-op 되고
    ///    숫자 집계(Stopwatch 기반)는 그대로 동작한다.
    ///
    /// 어떤 경우에도 예외를 던지지 않는다 — RuntimeInitializeOnLoadMethod 안에서 던지면
    /// 게임 부팅 전체가 깨질 수 있으므로 계측 코드 전체를 방어적으로 감싼다.
    /// </remarks>
    internal static class AITHookTimer
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __AITDebugLog_MarkStart(string hookName);

        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __AITDebugLog_MarkEnd(string hookName);
#endif

        private static readonly List<KeyValuePair<string, double>> _durationsMs =
            new List<KeyValuePair<string, double>>(8);

        /// <summary>
        /// 훅 실행 구간을 계측한다. 사용 예:
        /// <code>using var _ = AITHookTimer.Begin("Sentry");</code>
        /// using 선언 스코프가 끝나는 시점(메서드 정상 종료·조기 return 포함)에 자동으로 기록된다.
        /// </summary>
        internal static Scope Begin(string hookName)
        {
            try
            {
                // AIT_FIRST_INTERACTIVE_LOG 게이트 재사용 — 비활성이면 아래 어떤 작업도 하지 않는다.
                if (!AITPerformanceLogger.IsFirstInteractiveLogEnabled())
                    return default;

                return new Scope(hookName);
            }
            catch
            {
                // 게이트 조회 실패 등 어떤 이유로도 부팅을 막지 않는다.
                return default;
            }
        }

        /// <summary>
        /// 지금까지 기록된 훅별 소요 시간(ms) 스냅샷.
        /// AITPerformanceLogger가 first-interactive 리포트를 만들 때 1회 호출한다.
        /// </summary>
        internal static Dictionary<string, object> SnapshotMs()
        {
            var result = new Dictionary<string, object>(_durationsMs.Count);
            foreach (var kv in _durationsMs)
            {
                result[kv.Key] = kv.Value;
            }
            return result;
        }

        /// <summary>기록된 훅 소요 시간의 합계(ms).</summary>
        internal static double TotalMs()
        {
            double total = 0;
            foreach (var kv in _durationsMs)
            {
                total += kv.Value;
            }
            return Math.Round(total, 3);
        }

        private static void Record(string hookName, double elapsedMs)
        {
            try
            {
                _durationsMs.Add(new KeyValuePair<string, double>(hookName, Math.Round(elapsedMs, 3)));
            }
            catch
            {
                // 기록 실패는 무시 — 부팅에 영향 없음.
            }
        }

        private static void MarkStartSafe(string hookName)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            try { __AITDebugLog_MarkStart(hookName); } catch { /* devtools mark 실패는 무시 */ }
#endif
        }

        private static void MarkEndSafe(string hookName)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            try { __AITDebugLog_MarkEnd(hookName); } catch { /* devtools mark 실패는 무시 */ }
#endif
        }

        /// <summary>
        /// using 선언으로 훅 실행 구간을 감싸는 스코프.
        /// default(Scope)는 게이트 비활성 상태의 no-op(필드가 전부 null/0)이다.
        /// </summary>
        internal readonly struct Scope : IDisposable
        {
            private readonly string _hookName;
            private readonly long _startTimestamp;

            internal Scope(string hookName)
            {
                _hookName = hookName;
                _startTimestamp = Stopwatch.GetTimestamp();
                MarkStartSafe(hookName);
            }

            public void Dispose()
            {
                if (_hookName == null) return; // default(Scope) — 게이트 비활성, no-op

                try
                {
                    long elapsedTicks = Stopwatch.GetTimestamp() - _startTimestamp;
                    double elapsedMs = elapsedTicks * 1000.0 / Stopwatch.Frequency;
                    MarkEndSafe(_hookName);
                    Record(_hookName, elapsedMs);
                }
                catch
                {
                    // 계측 종료 처리 실패가 부팅을 막으면 안 된다.
                }
            }
        }
    }
}
