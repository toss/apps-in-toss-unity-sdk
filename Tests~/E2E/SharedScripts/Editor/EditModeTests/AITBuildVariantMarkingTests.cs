// -----------------------------------------------------------------------
// AITBuildVariantMarkingTests.cs - perf 번들 마킹(buildVariant) 회귀 가드
// Level 0: S0 상수 → S1 UNITY_METADATA JSON → S2 플레이스홀더 게이트 체인이
//          리팩터링으로 조용히 끊기지 않는지 검증 (빌드/파일시스템 비의존)
// -----------------------------------------------------------------------

using NUnit.Framework;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.TestTools;
using AppsInToss.Editor;

[TestFixture]
public class AITBuildVariantMarkingTests
{
    // S0: 커밋 상수 — 이 코드가 들어간 SDK로 빌드하면 항상 perf로 마킹된다.
    [Test]
    public void BuildVariantConstant_IsPerf()
    {
        Assert.AreEqual("perf", AITBuildVariant.Value);
    }

    // S1: .ait 헤더로 가는 UNITY_METADATA JSON에 buildVariant가 포함된다.
    [Test]
    public void UnityMetadataJson_ContainsBuildVariant()
    {
        string json = AITUnityMetadata.BuildMetadataJson();
        StringAssert.Contains("\"buildVariant\":\"perf\"", json);
    }

    // S1: granite로 전달되는 환경변수 딕셔너리에도 동일하게 실린다.
    [Test]
    public void UnityMetadataEnv_CarriesBuildVariant()
    {
        var env = AITUnityMetadata.BuildEnvironmentVariables();
        Assert.IsTrue(env.ContainsKey("UNITY_METADATA"));
        StringAssert.Contains("\"buildVariant\":\"perf\"", env["UNITY_METADATA"]);
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
