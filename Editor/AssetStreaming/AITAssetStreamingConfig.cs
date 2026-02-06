using UnityEditor;

namespace AppsInToss.Editor.AssetStreaming
{
    /// <summary>
    /// Asset Streaming Advisor 설정 (EditorPrefs 기반)
    /// </summary>
    public static class AITAssetStreamingConfig
    {
        /// <summary>
        /// Addressable 그룹 이름 상수
        /// </summary>
        public const string AddressableGroupName = "AIT-Streaming-Assets";

        private const string Prefix = "AIT_AssetStreaming_";
        private const string EnabledKey = Prefix + "Enabled";
        private const string DontShowAgainKey = Prefix + "DontShowAgain";
        private const string BuildSizeThresholdMBKey = Prefix + "BuildSizeThresholdMB";
        private const string MinAssetSizeKBKey = Prefix + "MinAssetSizeKB";

        /// <summary>
        /// 기능 활성화 여부 (기본: true)
        /// </summary>
        public static bool Enabled
        {
            get => EditorPrefs.GetBool(EnabledKey, true);
            set => EditorPrefs.SetBool(EnabledKey, value);
        }

        /// <summary>
        /// "다시 표시 안함" 플래그
        /// </summary>
        public static bool DontShowAgain
        {
            get => EditorPrefs.GetBool(DontShowAgainKey, false);
            set => EditorPrefs.SetBool(DontShowAgainKey, value);
        }

        /// <summary>
        /// 총 빌드 크기 임계값 (MB) - 이 크기 초과 시 권유 활성화 (기본: 50MB)
        /// </summary>
        public static float BuildSizeThresholdMB
        {
            get => EditorPrefs.GetFloat(BuildSizeThresholdMBKey, 50f);
            set => EditorPrefs.SetFloat(BuildSizeThresholdMBKey, value);
        }

        /// <summary>
        /// 개별 에셋 최소 크기 필터 (KB) - 이 크기 미만은 무시 (기본: 256KB)
        /// </summary>
        public static int MinAssetSizeKB
        {
            get => EditorPrefs.GetInt(MinAssetSizeKBKey, 256);
            set => EditorPrefs.SetInt(MinAssetSizeKBKey, value);
        }

        /// <summary>
        /// 최소 에셋 크기를 바이트로 반환
        /// </summary>
        public static long MinAssetSizeBytes => (long)MinAssetSizeKB * 1024;

        /// <summary>
        /// 설정 초기화
        /// </summary>
        public static void ResetToDefaults()
        {
            EditorPrefs.DeleteKey(EnabledKey);
            EditorPrefs.DeleteKey(DontShowAgainKey);
            EditorPrefs.DeleteKey(BuildSizeThresholdMBKey);
            EditorPrefs.DeleteKey(MinAssetSizeKBKey);
        }
    }
}
