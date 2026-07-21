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
}
