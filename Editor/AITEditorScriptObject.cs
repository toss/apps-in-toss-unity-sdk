using UnityEngine;

namespace AppsInToss
{
    /// <summary>
    /// Apps in Toss Editor 설정 오브젝트
    /// </summary>
    [System.Serializable]
    public class AITEditorScriptObject : ScriptableObject
    {
        [Header("앱 기본 정보")]
        public string appName = "";
        public string displayName = "";
        public string version = "0.0.1";
        public string description = "";

        [Header("브랜드 설정")]
        public string primaryColor = "#3182F6";
        public string iconUrl = "";

        [Header("개발 서버 설정")]
        public int localPort = 5173;

        [Header("빌드 설정")]
        public bool isProduction = false;
        public bool enableOptimization = true;
        public bool enableCompression = false;

        [Header("토스페이 설정")]
        public string tossPayMerchantId = "";
        public string tossPayClientKey = "";

        [Header("광고 설정")]
        public bool enableAdvertisement = false;
        public string interstitialAdGroupId = "ait-ad-test-interstitial-id";
        public string rewardedAdGroupId = "ait-ad-test-rewarded-id";

        [Header("배포 설정")]
        public string deploymentKey = "";

        [Header("권한 설정")]
        public string[] permissions = new string[] { "userInfo", "location", "camera" };

        [Header("플러그인 설정")]
        public string[] plugins = new string[] { };

        /// <summary>
        /// 아이콘 URL 유효성 검사
        /// </summary>
        public bool IsIconUrlValid()
        {
            return !string.IsNullOrWhiteSpace(iconUrl) &&
                   (iconUrl.StartsWith("http://") || iconUrl.StartsWith("https://"));
        }

        /// <summary>
        /// 앱 ID 유효성 검사
        /// </summary>
        public bool IsAppNameValid()
        {
            if (string.IsNullOrWhiteSpace(appName))
                return false;

            // 영문, 숫자, 하이픈만 허용
            foreach (char c in appName)
            {
                if (!char.IsLetterOrDigit(c) && c != '-')
                    return false;
            }

            return true;
        }

        /// <summary>
        /// 버전 형식 검사 (x.y.z)
        /// </summary>
        public bool IsVersionValid()
        {
            if (string.IsNullOrWhiteSpace(version))
                return false;

            string[] parts = version.Split('.');
            if (parts.Length != 3)
                return false;

            foreach (string part in parts)
            {
                if (!int.TryParse(part, out _))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// 배포 준비 완료 여부
        /// </summary>
        public bool IsReadyForDeploy()
        {
            return IsIconUrlValid() &&
                   IsAppNameValid() &&
                   IsVersionValid() &&
                   !string.IsNullOrWhiteSpace(deploymentKey);
        }
    }
}
