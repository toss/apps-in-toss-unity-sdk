namespace AppsInToss.Editor
{
    /// <summary>
    /// 빌드 변형(variant) 식별자 — 번들 마킹의 단일 진실원천.
    ///
    /// 이 값은 .ait 헤더(<see cref="AITUnityMetadata.BuildMetadataJson"/>의 buildVariant 필드)와
    /// in-page JS(window.AITLoading.buildVariant, %AIT_BUILD_VARIANT% 치환)에 동시에 주입되어
    /// 배포·분석에서 이 SDK로 생성된 번들을 식별·귀속하는 데 쓰입니다.
    ///
    /// 기본값은 빈 문자열입니다 — stable/비채널 빌드는 buildVariant 없이 빈 문자열로 마킹되며,
    /// 이는 main 브랜치 산출 번들과 동일한 마킹입니다. 채널(perf 등) 빌드로 마킹하려면 빌드 시
    /// env가 아니라 beta-release.yml의 "베타 준비" staging 단계가 workflow_dispatch 입력
    /// build_variant 값으로 이 상수를 치환합니다 — 파트너가 채널 브랜치 소스를 로컬에서 직접
    /// 빌드하므로 커밋된 소스 자체에 마킹이 있어야 하기 때문입니다. 아래 라인의 주석
    /// (AIT_BUILD_VARIANT_INJECT)은 그 sed 치환이 앵커로 삼는 지점이므로 라인 형태를 임의로
    /// 바꾸지 마세요.
    /// </summary>
    internal static class AITBuildVariant
    {
        /// <summary>빌드 변형 식별자. 빈 문자열 = stable/비채널 빌드.</summary>
        internal const string Value = ""; // beta-release staging 치환 지점 (AIT_BUILD_VARIANT_INJECT)
    }
}
