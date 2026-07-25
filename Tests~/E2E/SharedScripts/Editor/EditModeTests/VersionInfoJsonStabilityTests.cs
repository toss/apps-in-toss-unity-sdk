// -----------------------------------------------------------------------
// VersionInfoJsonStabilityTests.cs
// Level 0: AITWebGLBuilder.BuildVersionInfoJson — 버전 정보 JSON 안정화 검증
// (version+commitHash 불변 시 기존 releaseDateTime 재사용 → 내용 불변 → 재기록 스킵
//  → 파일/.meta 안정 → 무변경 재빌드에서 data 아카이브 재빌드+재압축 방지)
// -----------------------------------------------------------------------

using NUnit.Framework;
using AppsInToss.Editor;

[TestFixture]
public class VersionInfoJsonStabilityTests
{
    [Test]
    public void NoExistingFile_ProducesJsonWithGivenValues_Changed()
    {
        string json = AITWebGLBuilder.BuildVersionInfoJson(
            null, "1.9.0", "abc1234", "20260725_1200", out bool changed);

        Assert.IsTrue(changed);
        StringAssert.Contains("\"version\": \"1.9.0\"", json);
        StringAssert.Contains("\"releaseDateTime\": \"20260725_1200\"", json);
        StringAssert.Contains("\"commitHash\": \"abc1234\"", json);
    }

    [Test]
    public void SameVersionAndCommit_ReusesExistingStamp_NotChanged()
    {
        string first = AITWebGLBuilder.BuildVersionInfoJson(
            null, "1.9.0", "abc1234", "20260725_1200", out _);

        // 다음 빌드: wall-clock은 달라졌지만 버전·커밋은 그대로
        string second = AITWebGLBuilder.BuildVersionInfoJson(
            first, "1.9.0", "abc1234", "20260725_1830", out bool changed);

        Assert.IsFalse(changed, "버전·커밋 불변이면 재기록 없이 기존 내용 유지 (data 캐시 보존의 핵심)");
        Assert.AreEqual(first, second, "재사용 시 기존 파일과 바이트 동일한 canonical JSON이어야 함");
        StringAssert.Contains("\"releaseDateTime\": \"20260725_1200\"", second);
    }

    [Test]
    public void DifferentCommitHash_UsesFreshStamp_Changed()
    {
        string first = AITWebGLBuilder.BuildVersionInfoJson(
            null, "1.9.0", "abc1234", "20260725_1200", out _);

        string second = AITWebGLBuilder.BuildVersionInfoJson(
            first, "1.9.0", "def5678", "20260726_0900", out bool changed);

        Assert.IsTrue(changed, "커밋이 바뀌면 재기록 (그때의 data 재빌드는 정당한 비용)");
        StringAssert.Contains("\"commitHash\": \"def5678\"", second);
        StringAssert.Contains("\"releaseDateTime\": \"20260726_0900\"", second);
    }

    [Test]
    public void DifferentVersion_UsesFreshStamp_Changed()
    {
        string first = AITWebGLBuilder.BuildVersionInfoJson(
            null, "1.9.0", "abc1234", "20260725_1200", out _);

        string second = AITWebGLBuilder.BuildVersionInfoJson(
            first, "1.10.0", "abc1234", "20260726_0900", out bool changed);

        Assert.IsTrue(changed);
        StringAssert.Contains("\"version\": \"1.10.0\"", second);
        StringAssert.Contains("\"releaseDateTime\": \"20260726_0900\"", second);
    }

    [Test]
    public void MalformedExistingJson_RewritesCanonical_Changed()
    {
        string json = AITWebGLBuilder.BuildVersionInfoJson(
            "{ not valid json", "1.9.0", "abc1234", "20260725_1200", out bool changed);

        Assert.IsTrue(changed);
        StringAssert.Contains("\"version\": \"1.9.0\"", json);
    }

    [Test]
    public void ExistingWithEmptyStamp_UsesFreshStamp_Changed()
    {
        string existing = "{\"version\":\"1.9.0\",\"releaseDateTime\":\"\",\"commitHash\":\"abc1234\"}";

        string json = AITWebGLBuilder.BuildVersionInfoJson(
            existing, "1.9.0", "abc1234", "20260725_1200", out bool changed);

        Assert.IsTrue(changed);
        StringAssert.Contains("\"releaseDateTime\": \"20260725_1200\"", json);
    }

    [Test]
    public void NonCanonicalFormatting_SameValues_RewrittenOnce_ThenStable()
    {
        // 값은 같지만 포맷이 다른(비 prettyPrint) 기존 파일 → 1회 canonical 재기록
        string nonCanonical = "{\"version\":\"1.9.0\",\"releaseDateTime\":\"20260725_1200\",\"commitHash\":\"abc1234\"}";

        string rewritten = AITWebGLBuilder.BuildVersionInfoJson(
            nonCanonical, "1.9.0", "abc1234", "20260726_0900", out bool changedFirst);

        Assert.IsTrue(changedFirst, "포맷이 다르면 canonical 형태로 1회 재기록");
        StringAssert.Contains("\"releaseDateTime\": \"20260725_1200\"", rewritten);

        // 이후 빌드부터는 안정
        AITWebGLBuilder.BuildVersionInfoJson(
            rewritten, "1.9.0", "abc1234", "20260727_1500", out bool changedSecond);
        Assert.IsFalse(changedSecond);
    }
}
