using System;
using UnityEngine;
using AppsInToss.Editor.ErrorTracker;

namespace AppsInToss.Editor
{
    /// <summary>
    /// Debug.Log/LogWarning/LogError 래퍼.
    /// sentryCapture 옵션으로 Sentry 전송 여부를 로그 레벨과 독립적으로 제어합니다.
    ///
    /// 사용 예시:
    ///   AITLog.Error("빌드 실패", sentryCapture: true);   // Console Error + Sentry 전송
    ///   AITLog.Error("설정 누락", sentryCapture: false);  // Console Error만 (Sentry 전송 안 함)
    ///   AITLog.Warning("캐시 미스", sentryCapture: true);  // Console Warning + Sentry 전송
    /// </summary>
    internal static class AITLog
    {
        /// <summary>
        /// Sentry 캡처를 일시 억제하는 스코프.
        /// using 문과 함께 사용하여 try-finally 패턴을 보장합니다.
        /// </summary>
        internal struct SuppressScope : IDisposable
        {
            private readonly bool _active;

            internal SuppressScope(bool active)
            {
                _active = active;
                if (_active)
                    AITEditorErrorTracker.BeginSuppressLogCapture();
            }

            public void Dispose()
            {
                if (_active)
                    AITEditorErrorTracker.EndSuppressLogCapture();
            }
        }

        /// <summary>
        /// Debug.Log를 호출합니다. Sentry에는 전송되지 않습니다 (Info 레벨은 수집 대상 아님).
        /// </summary>
        internal static void Info(string message)
        {
            Debug.Log(message);
        }

        /// <summary>
        /// Debug.LogWarning을 호출합니다.
        /// sentryCapture가 false이면 Sentry 전송을 억제합니다.
        /// </summary>
        internal static void Warning(string message, bool sentryCapture = true)
        {
            using (new SuppressScope(!sentryCapture))
            {
                Debug.LogWarning(message);
            }
        }

        /// <summary>
        /// Debug.LogError를 호출합니다.
        /// sentryCapture가 false이면 Sentry 전송을 억제합니다.
        /// </summary>
        internal static void Error(string message, bool sentryCapture = true)
        {
            using (new SuppressScope(!sentryCapture))
            {
                Debug.LogError(message);
            }
        }
    }
}
