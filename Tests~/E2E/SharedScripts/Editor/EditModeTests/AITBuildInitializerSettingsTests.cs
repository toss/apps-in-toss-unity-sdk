// -----------------------------------------------------------------------
// AITBuildInitializerSettingsTests.cs - Sentry/Stack Trace 설정 자동 적용 검증
// Level 0: AITBuildInitializer.ApplySentryFriendlyWebGLSettings 및 백업/복원 순수 로직 검증
// Sentry 이슈 APPS-IN-TOSS-UNITY-SDK-8A / 8B 재발 방지용 회귀 테스트
// -----------------------------------------------------------------------

using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using AppsInToss;
using AppsInToss.Editor;

[TestFixture]
public class AITBuildInitializerSettingsTests
{
    private PlayerSettingsSnapshot backup;

    [SetUp]
    public void Setup()
    {
        // 테스트 실행 중 PlayerSettings를 변경하므로 미리 백업
        backup = PlayerSettingsSnapshot.Capture();
    }

    [TearDown]
    public void TearDown()
    {
        // 테스트 종료 후 원래 PlayerSettings로 복원
        backup.Restore();
    }

    // =====================================================
    // 기본값 검증 (SDK-8A 회귀 방지)
    // =====================================================

    [Test]
    public void GetDefaultExceptionSupport_Returns_FullWithStacktrace()
    {
        // Sentry가 stack trace를 캡처하려면 FullWithStacktrace 필요
        // 이 값이 낮아지면 Sentry에서 SDK-8A 경고가 재발생함
        var result = AITDefaultSettings.GetDefaultExceptionSupport();
        Assert.AreEqual(WebGLExceptionSupport.FullWithStacktrace, result,
            "Default exception support must be FullWithStacktrace to avoid Sentry SDK-8A warning");
    }

    // =====================================================
    // ApplySentryFriendlyWebGLSettings 동작 검증 (SDK-8A 회귀 방지)
    // =====================================================

    [Test]
    public void ApplySentryFriendlyWebGLSettings_Sets_ExceptionSupport()
    {
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.None;

        AITBuildInitializer.ApplySentryFriendlyWebGLSettings(WebGLExceptionSupport.FullWithStacktrace);

        Assert.AreEqual(WebGLExceptionSupport.FullWithStacktrace,
            PlayerSettings.WebGL.exceptionSupport,
            "ApplySentryFriendlyWebGLSettings must set WebGL exceptionSupport to the requested value");
    }

    // =====================================================
    // ApplySentryFriendlyWebGLSettings Stack Trace 설정 검증 (SDK-8B 회귀 방지)
    // =====================================================

    [TestCase(LogType.Error)]
    [TestCase(LogType.Assert)]
    [TestCase(LogType.Warning)]
    [TestCase(LogType.Log)]
    [TestCase(LogType.Exception)]
    public void ApplySentryFriendlyWebGLSettings_Sets_StackTraceLogType_ScriptOnly(LogType logType)
    {
        // WebGL에서 지원되지 않는 Full로 먼저 설정 → 헬퍼가 ScriptOnly로 내려야 함
        PlayerSettings.SetStackTraceLogType(logType, StackTraceLogType.Full);

        AITBuildInitializer.ApplySentryFriendlyWebGLSettings(WebGLExceptionSupport.FullWithStacktrace);

        Assert.AreEqual(StackTraceLogType.ScriptOnly,
            PlayerSettings.GetStackTraceLogType(logType),
            $"ApplySentryFriendlyWebGLSettings must set stack trace log type for {logType} to ScriptOnly (WebGL does not support Full)");
    }

    // =====================================================
    // Backup/Restore가 Stack Trace 설정을 보존하는지 확인
    // =====================================================

    [Test]
    public void PlayerSettingsBackup_RoundTrips_StackTraceLogType()
    {
        // 사용자 설정 시뮬레이션: Full로 지정
        PlayerSettings.SetStackTraceLogType(LogType.Error, StackTraceLogType.Full);
        PlayerSettings.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);

        var snapshot = PlayerSettingsSnapshot.Capture();

        // SDK가 값을 덮어씀
        PlayerSettings.SetStackTraceLogType(LogType.Error, StackTraceLogType.ScriptOnly);
        PlayerSettings.SetStackTraceLogType(LogType.Log, StackTraceLogType.ScriptOnly);

        // 덮어쓰기가 실제로 snapshot 값과 다른지 확인해 Restore가 fixed-point가 아니라 round-trip임을 보장
        Assert.AreNotEqual(StackTraceLogType.Full,
            PlayerSettings.GetStackTraceLogType(LogType.Error),
            "Sanity: overwrite must actually change LogType.Error value before Restore");

        snapshot.Restore();

        Assert.AreEqual(StackTraceLogType.Full,
            PlayerSettings.GetStackTraceLogType(LogType.Error),
            "Restore must return LogType.Error stack trace to pre-build user value");
        Assert.AreEqual(StackTraceLogType.None,
            PlayerSettings.GetStackTraceLogType(LogType.Log),
            "Restore must return LogType.Log stack trace to pre-build user value");
    }

    // =====================================================
    // ApplyShowUnityLogoSetting 동작 검증 (죽은 설정 배선 회귀 방지)
    // =====================================================

    /// <summary>
    /// Personal 라이선스 에디터에서는 PlayerSettings.SplashScreen.show=false 대입이
    /// 조용히 무시된다(값이 true로 유지됨 — Plus/Pro 전용 기능). setter가 실제로 반영되는
    /// 환경인지 프로브해, 반영을 전제하는 테스트를 라이선스와 무관하게 안전하게 게이트한다.
    /// </summary>
    private static bool SplashScreenSetterIsEffective()
    {
        bool originalShow = PlayerSettings.SplashScreen.show;
        try
        {
            PlayerSettings.SplashScreen.show = false;
            return PlayerSettings.SplashScreen.show == false;
        }
        finally
        {
            PlayerSettings.SplashScreen.show = originalShow;
        }
    }

    [Test]
    public void ApplyShowUnityLogoSetting_ShowRequested_LeavesSplashScreenUntouched()
    {
        bool beforeShow = PlayerSettings.SplashScreen.show;
        bool beforeLogo = PlayerSettings.SplashScreen.showUnityLogo;

        // 로고 표시가 요청되면(showUnityLogoResolved=true) 라이선스와 무관하게 아무것도 쓰지 않는다.
        bool applied = AITBuildInitializer.ApplyShowUnityLogoSetting(showUnityLogoResolved: true, hasProLicense: true);

        Assert.IsFalse(applied, "표시 요청 시에는 적용(hide)이 일어나지 않아야 한다");
        Assert.AreEqual(beforeShow, PlayerSettings.SplashScreen.show, "표시 요청 시 기존 PlayerSettings 값을 건드리면 안 된다");
        Assert.AreEqual(beforeLogo, PlayerSettings.SplashScreen.showUnityLogo, "표시 요청 시 기존 PlayerSettings 값을 건드리면 안 된다");
    }

    [Test]
    public void ApplyShowUnityLogoSetting_HideRequested_WithProLicense_HidesLogo()
    {
        if (!SplashScreenSetterIsEffective())
        {
            Assert.Ignore("Personal 라이선스 에디터에서는 SplashScreen setter가 무시되어 적용 결과를 검증할 수 없습니다.");
            return;
        }

        bool originalShow = PlayerSettings.SplashScreen.show;
        bool originalLogo = PlayerSettings.SplashScreen.showUnityLogo;
        try
        {
            PlayerSettings.SplashScreen.show = true;
            PlayerSettings.SplashScreen.showUnityLogo = true;

            bool applied = AITBuildInitializer.ApplyShowUnityLogoSetting(showUnityLogoResolved: false, hasProLicense: true);

            Assert.IsTrue(applied, "Pro 라이선스가 있으면 로고 숨김이 적용되어야 한다");
            Assert.IsFalse(PlayerSettings.SplashScreen.show);
            Assert.IsFalse(PlayerSettings.SplashScreen.showUnityLogo);
        }
        finally
        {
            PlayerSettings.SplashScreen.show = originalShow;
            PlayerSettings.SplashScreen.showUnityLogo = originalLogo;
        }
    }

    [Test]
    public void ApplyShowUnityLogoSetting_HideRequested_WithoutProLicense_SkipsAndLeavesSettingsUntouched()
    {
        PlayerSettings.SplashScreen.show = true;
        PlayerSettings.SplashScreen.showUnityLogo = true;

        // Personal 라이선스는 Unity가 로고 표시를 강제하므로 시도 자체를 건너뛰어야 한다.
        bool applied = AITBuildInitializer.ApplyShowUnityLogoSetting(showUnityLogoResolved: false, hasProLicense: false);

        Assert.IsFalse(applied, "Pro 라이선스가 없으면 숨김을 적용하면 안 된다");
        Assert.IsTrue(PlayerSettings.SplashScreen.show, "Personal 라이선스에서는 원래 값을 그대로 두어야 한다");
        Assert.IsTrue(PlayerSettings.SplashScreen.showUnityLogo, "Personal 라이선스에서는 원래 값을 그대로 두어야 한다");
    }
}
