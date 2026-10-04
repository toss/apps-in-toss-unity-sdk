// -----------------------------------------------------------------------
// AITBuildVariantMarkingTests.cs - 채널 번들 마킹(buildVariant) 회귀 가드
// Level 0: S0 상수 → S1 UNITY_METADATA JSON → S2 플레이스홀더 게이트 체인이
//          리팩터링으로 조용히 끊기지 않는지 검증 (빌드/파일시스템 비의존)
// 채널 마킹(perf 등)은 커밋 상수가 아니라 beta-release.yml staging 단계의
// sed 치환으로 주입되므로(AIT_BUILD_VARIANT_INJECT), 이 테스트가 보는 소스 트리
// 기준 기본값은 빈 문자열(stable/비채널)이다.
// -----------------------------------------------------------------------

using NUnit.Framework;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.TestTools;
using AppsInToss.Editor;

[TestFixture]
public class AITBuildVariantMarkingTests
{
    // S0: 기본값은 빈 문자열 — stable/비채널 빌드는 buildVariant 없이 마킹된다.
    [Test]
    public void BuildVariantConstant_DefaultsToEmpty()
    {
        Assert.AreEqual("", AITBuildVariant.Value);
    }

    // S1: .ait 헤더로 가는 UNITY_METADATA JSON에 buildVariant가 포함된다.
    [Test]
    public void UnityMetadataJson_ContainsBuildVariant()
    {
        string json = AITUnityMetadata.BuildMetadataJson();
        StringAssert.Contains("\"buildVariant\":\"\"", json);
    }

    // S1: granite로 전달되는 환경변수 딕셔너리에도 동일하게 실린다.
    [Test]
    public void UnityMetadataEnv_CarriesBuildVariant()
    {
        var env = AITUnityMetadata.BuildEnvironmentVariables();
        Assert.IsTrue(env.ContainsKey("UNITY_METADATA"));
        StringAssert.Contains("\"buildVariant\":\"\"", env["UNITY_METADATA"]);
    }

    // S2: %AIT_BUILD_VARIANT% 미치환은 이제 치명적 플레이스홀더 게이트에 걸린다.
    [Test]
    public void UnsubstitutedPlaceholder_FailsBuildValidation()
    {
        LogAssert.Expect(LogType.Error, new Regex("AIT_BUILD_VARIANT"));
        bool ok = AITBuildValidator.ValidatePlaceholderSubstitution(
            "window.AITLoading = { buildVariant: '%AIT_BUILD_VARIANT%' };", "index.html");
        Assert.IsFalse(ok);
    }

    // S2: 정상 치환된 컨텐츠는 게이트를 통과한다.
    [Test]
    public void SubstitutedContent_PassesBuildValidation()
    {
        bool ok = AITBuildValidator.ValidatePlaceholderSubstitution(
            "window.AITLoading = { buildVariant: 'perf' };", "index.html");
        Assert.IsTrue(ok);
    }
}
