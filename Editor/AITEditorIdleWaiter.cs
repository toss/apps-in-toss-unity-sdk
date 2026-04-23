using System;
using System.Threading.Tasks;
using UnityEditor;

namespace AppsInToss.Editor
{
    /// <summary>
    /// 빌드 진입 전 Unity 가 idle 상태(!isCompiling && !isUpdating) 가 될 때까지
    /// 대기. 타임아웃 시 사용자에게 계속 진행 여부를 물음.
    /// 테스트 가능성을 위해 probe 들을 정적 필드로 주입 가능.
    /// </summary>
    internal static class AITEditorIdleWaiter
    {
        private static Func<bool> _isCompilingProbe = () => EditorApplication.isCompiling;
        private static Func<bool> _isUpdatingProbe  = () => EditorApplication.isUpdating;
        private static Func<string, bool> _timeoutDialog = DefaultTimeoutDialog;

        // 폴링 간격 — 테스트에서는 짧게, 실제에서는 200ms
        private static int _pollIntervalMs = 200;

        public static async Task<bool> WaitAsync(int timeoutSeconds = 60)
        {
            var start = DateTime.UtcNow;
            var timeout = TimeSpan.FromSeconds(timeoutSeconds);

            while (_isCompilingProbe() || _isUpdatingProbe())
            {
                if (DateTime.UtcNow - start > timeout)
                {
                    string reason = _isCompilingProbe() ? "스크립트 컴파일" : "에셋 임포트";
                    return _timeoutDialog(reason);
                }
                await Task.Delay(_pollIntervalMs);
            }
            return true;
        }

        private static bool DefaultTimeoutDialog(string reason)
        {
            return EditorUtility.DisplayDialog(
                "Unity 가 계속 작업 중",
                $"{reason} 이(가) 60초 넘게 진행 중입니다. 그래도 빌드를 시작할까요?",
                "시작",
                "취소");
        }

#if UNITY_INCLUDE_TESTS
        internal static void SetProbesForTesting(
            Func<bool> isCompiling,
            Func<bool> isUpdating,
            Func<string, bool> timeoutDialog,
            int pollIntervalMs = 10)
        {
            _isCompilingProbe = isCompiling;
            _isUpdatingProbe = isUpdating;
            _timeoutDialog = timeoutDialog;
            _pollIntervalMs = pollIntervalMs;
        }

        internal static void ResetProbesForTesting()
        {
            _isCompilingProbe = () => EditorApplication.isCompiling;
            _isUpdatingProbe  = () => EditorApplication.isUpdating;
            _timeoutDialog = DefaultTimeoutDialog;
            _pollIntervalMs = 200;
        }
#endif
    }
}
