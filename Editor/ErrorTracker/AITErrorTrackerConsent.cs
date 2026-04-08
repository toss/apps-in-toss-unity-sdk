using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AppsInToss.Editor.ErrorTracker
{
    /// <summary>
    /// 에러 트래커 활성화 상태 관리.
    /// 기본 활성 (opt-out 방식) — 설정 윈도우에서 비활성화할 수 있습니다.
    /// </summary>
    internal static class AITErrorTrackerConsent
    {
        private const string EnabledKey = "AIT_ErrorTracker_Enabled";
        private const string NotifiedKey = "AIT_ErrorTracker_Notified";
        private const string DistinctIdKey = "AIT_ErrorTracker_DistinctId";

        /// <summary>
        /// 에러 트래킹이 활성화되어 있는지 확인합니다.
        /// 기본값: true (opt-out 방식)
        /// </summary>
        internal static bool IsEnabled()
        {
            // EditorPrefs에 키가 없으면 기본 활성
            return EditorPrefs.GetBool(EnabledKey, true);
        }

        /// <summary>
        /// 에러 트래킹 활성화/비활성화 설정
        /// </summary>
        internal static void SetEnabled(bool enabled)
        {
            EditorPrefs.SetBool(EnabledKey, enabled);
        }

        /// <summary>
        /// 최초 1회 고지 다이얼로그 표시.
        /// 수집 사실을 알리고, 설정에서 비활성화할 수 있음을 안내합니다.
        /// </summary>
        internal static void ShowNoticeIfNeeded()
        {
            if (EditorPrefs.GetBool(NotifiedKey, false))
                return;

            // CI/배치 모드에서는 고지 생략
            if (AITPlatformHelper.IsNonInteractive)
            {
                EditorPrefs.SetBool(NotifiedKey, true);
                return;
            }

            EditorUtility.DisplayDialog(
                "Apps in Toss SDK",
                "SDK 안정성 향상을 위해 익명 에러 데이터를 수집합니다.\n\n"
                + "수집 항목: 에러 메시지, Unity 버전, OS, SDK 버전\n"
                + "미수집 항목: 개인정보, 소스 코드, 게임 콘텐츠\n\n"
                + "Apps in Toss > Settings에서 비활성화할 수 있습니다.",
                "확인");

            EditorPrefs.SetBool(NotifiedKey, true);
        }

        /// <summary>
        /// 익명 Distinct ID 반환 (세션 추적용).
        /// 머신 고유 식별자를 SHA256 해싱하여 역추적 불가능합니다.
        /// </summary>
        internal static string GetDistinctId()
        {
            string cached = EditorPrefs.GetString(DistinctIdKey, "");
            if (!string.IsNullOrEmpty(cached))
                return cached;

            string deviceId = SystemInfo.deviceUniqueIdentifier;
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(deviceId));
                StringBuilder sb = new StringBuilder(32);
                for (int i = 0; i < hash.Length; i++)
                {
                    sb.Append(hash[i].ToString("x2"));
                }

                string distinctId = sb.ToString().Substring(0, 16);
                EditorPrefs.SetString(DistinctIdKey, distinctId);
                return distinctId;
            }
        }

        /// <summary>
        /// 모든 설정 초기화 (디버그/테스트용)
        /// </summary>
        internal static void Reset()
        {
            EditorPrefs.DeleteKey(EnabledKey);
            EditorPrefs.DeleteKey(NotifiedKey);
            EditorPrefs.DeleteKey(DistinctIdKey);
        }
    }
}
