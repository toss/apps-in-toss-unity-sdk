using UnityEditor;
using UnityEngine;

namespace AppsInToss.Editor.AssetStreaming
{
    /// <summary>
    /// 빌드 전 에셋 스트리밍 분석 체크
    /// 대용량 에셋이 있을 경우 사용자에게 Advisor 윈도우를 안내
    /// </summary>
    public static class AITPreBuildAssetCheck
    {
        /// <summary>
        /// 빌드 전 체크 실행
        /// </summary>
        /// <returns>true: 빌드 계속, false: 빌드 취소 (Advisor 윈도우 열기)</returns>
        public static bool RunPreBuildCheck()
        {
            // 기능 비활성화 또는 "다시 표시 안함"
            if (!AITAssetStreamingConfig.Enabled || AITAssetStreamingConfig.DontShowAgain)
            {
                return true;
            }

            // 빠른 분석
            var summary = AITAssetAnalyzer.QuickAnalyze();

            // 예상 절감이 임계값 미만이면 스킵
            float estimatedSavingsMB = summary.totalEstimatedSavingsBytes / (1024f * 1024f);
            if (estimatedSavingsMB < AITAssetStreamingConfig.BuildSizeThresholdMB)
            {
                return true;
            }

            // 다이얼로그 표시
            string message = $"프로젝트에 Addressable로 분리 가능한 대용량 에셋이 감지되었습니다.\n\n" +
                             $"  분리 가능 에셋: {summary.totalAssetCount}개\n" +
                             $"  예상 절감: {AITBuildValidator.FormatFileSize(summary.totalEstimatedSavingsBytes)}\n" +
                             $"  강력 추천: {summary.highlyRecommendedCount}개\n\n" +
                             $"에셋을 Addressable로 변환하면 초기 다운로드 크기를 줄일 수 있습니다.";

            int choice = AITPlatformHelper.ShowComplexDialog(
                "에셋 스트리밍 권장",
                message,
                "빌드 계속",       // 0
                "분석 창 열기",    // 1
                "다시 표시 안함",  // 2
                defaultChoice: 0); // CI에서는 빌드 계속

            switch (choice)
            {
                case 0: // 빌드 계속
                    return true;

                case 1: // 분석 창 열기
                    AITAssetStreamingAdvisorWindow.ShowWindow();
                    return false;

                case 2: // 다시 표시 안함
                    AITAssetStreamingConfig.DontShowAgain = true;
                    return true;

                default:
                    return true;
            }
        }
    }
}
