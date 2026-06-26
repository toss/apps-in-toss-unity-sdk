// ---------------------------------------------------------------------------
// EditorDsnResolverTests.cs - 에디터 트래커 DSN 환경변수 override 단위 테스트
// GetDsn()/IsDsnConfigured가 AIT_EDITOR_SENTRY_DSN 환경변수로 재정의 가능하면서도,
// 미설정 시에는 하드코딩 DEFAULT_DSN으로 폴백해 기본 동작이 불변(zero-default-change)인지 검증.
// ---------------------------------------------------------------------------

using System;
using NUnit.Framework;
using AppsInToss.Editor.ErrorTracker;

[TestFixture]
[Category("Unit")]
public class EditorDsnResolverTests
{
    private const string EnvVar = "AIT_EDITOR_SENTRY_DSN";
    private const string SampleOverrideDsn = "https://example0123456789abcdef@o12345.ingest.us.sentry.io/9999999";

    #region 순수 함수 ResolveEditorDsn — override 적용 규칙

    [Test]
    public void ResolveEditorDsn_NullOverride_FallsBackToDefault()
    {
        // 미설정(null) → DEFAULT_DSN. 기본 동작 불변의 핵심.
        var def = AITEditorErrorTracker.ResolveEditorDsn(null);
        Assert.IsFalse(string.IsNullOrEmpty(def), "기본 DSN은 비어있지 않아야 한다.");
    }

    [Test]
    public void ResolveEditorDsn_EmptyOrWhitespaceOverride_FallsBackToDefault()
    {
        // 빈 문자열/공백은 override로 취급하지 않고 DEFAULT_DSN으로 폴백한다.
        var def = AITEditorErrorTracker.ResolveEditorDsn(null);
        Assert.AreEqual(def, AITEditorErrorTracker.ResolveEditorDsn(""));
        Assert.AreEqual(def, AITEditorErrorTracker.ResolveEditorDsn("   "));
        Assert.AreEqual(def, AITEditorErrorTracker.ResolveEditorDsn("\t\n"));
    }

    [Test]
    public void ResolveEditorDsn_NonEmptyOverride_ReturnsOverride()
    {
        Assert.AreEqual(SampleOverrideDsn, AITEditorErrorTracker.ResolveEditorDsn(SampleOverrideDsn));
    }

    [Test]
    public void ResolveEditorDsn_TrimsSurroundingWhitespace()
    {
        Assert.AreEqual(SampleOverrideDsn, AITEditorErrorTracker.ResolveEditorDsn("  " + SampleOverrideDsn + "\n"));
    }

    [Test]
    public void ResolveEditorDsn_OverrideDiffersFromDefault()
    {
        // 샘플 override가 실제로 기본값과 다른 채널을 가리키는지(테스트가 자명참이 아닌지) 확인.
        var def = AITEditorErrorTracker.ResolveEditorDsn(null);
        Assert.AreNotEqual(def, SampleOverrideDsn);
    }

    #endregion

    #region GetDsn()/IsDsnConfigured — 환경변수 라운드트립

    [Test]
    public void GetDsn_WhenEnvUnset_ReturnsDefault()
    {
        var original = Environment.GetEnvironmentVariable(EnvVar);
        try
        {
            Environment.SetEnvironmentVariable(EnvVar, null);
            var def = AITEditorErrorTracker.ResolveEditorDsn(null);
            Assert.AreEqual(def, AITEditorErrorTracker.GetDsn());
            Assert.IsTrue(AITEditorErrorTracker.IsDsnConfigured);
        }
        finally
        {
            Environment.SetEnvironmentVariable(EnvVar, original);
        }
    }

    [Test]
    public void GetDsn_WhenEnvSet_ReturnsOverride()
    {
        var original = Environment.GetEnvironmentVariable(EnvVar);
        try
        {
            Environment.SetEnvironmentVariable(EnvVar, SampleOverrideDsn);
            Assert.AreEqual(SampleOverrideDsn, AITEditorErrorTracker.GetDsn());
            Assert.IsTrue(AITEditorErrorTracker.IsDsnConfigured);
        }
        finally
        {
            Environment.SetEnvironmentVariable(EnvVar, original);
        }
    }

    [Test]
    public void GetDsn_WhenEnvSetToWhitespace_FallsBackToDefault()
    {
        var original = Environment.GetEnvironmentVariable(EnvVar);
        try
        {
            Environment.SetEnvironmentVariable(EnvVar, "   ");
            var def = AITEditorErrorTracker.ResolveEditorDsn(null);
            Assert.AreEqual(def, AITEditorErrorTracker.GetDsn());
            Assert.IsTrue(AITEditorErrorTracker.IsDsnConfigured);
        }
        finally
        {
            Environment.SetEnvironmentVariable(EnvVar, original);
        }
    }

    #endregion
}
