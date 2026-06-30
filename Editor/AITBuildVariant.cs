namespace AppsInToss.Editor
{
    /// <summary>
    /// 빌드 변형(variant) 식별자 — 번들 마킹의 단일 진실원천.
    ///
    /// 이 값은 .ait 헤더(<see cref="AITUnityMetadata.BuildMetadataJson"/>의 buildVariant 필드)와
    /// in-page JS(window.AITLoading.buildVariant, %AIT_BUILD_VARIANT% 치환)에 동시에 주입되어
    /// 배포·분석에서 이 SDK로 생성된 번들을 식별·귀속하는 데 쓰입니다.
    ///
    /// 커밋 상수로 두는 이유: 제휴사 빌드 환경/워크플로의 버전 bump 에 비의존하며,
    /// 이 perf release-base 브랜치에만 존재해 제휴사가 임의로 바꿀 수 없는 본질적 표식이 됩니다.
    /// (main 브랜치에는 이 파일이 없으므로 main 산출 번들은 buildVariant 없이 빈 문자열로 마킹됩니다.)
    /// </summary>
    internal static class AITBuildVariant
    {
        /// <summary>perf 채널 빌드 식별자.</summary>
        internal const string Value = "perf";
    }
}
