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
    // 기본값 검증 (wasm 크기 회귀 방지)
    // =====================================================

    [Test]
    public void GetDefaultExceptionSupport_Returns_FullWithoutStacktrace()
    {
        // ExplicitlyThrownExceptionsOnly는 null 접근을 wasm trap/메모리 손상으로 만든다. 자동 기본값은 FullWithoutStacktrace여야 한다.
        // FullWithStacktrace는 wasm이 크게 커지므로(brotli 약 +30%) 사용자가 명시할 때만 쓴다.
        var result = AITDefaultSettings.GetDefaultExceptionSupport();
        Assert.AreEqual(WebGLExceptionSupport.FullWithoutStacktrace, result,
            "Auto exception support must be FullWithoutStacktrace (Explicit-only turns null access into wasm traps; FullWithStacktrace greatly increases wasm size)");
    }

    [Test]
    public void ResolveExceptionSupport_Auto_UsesFullWithoutStacktrace_ExplicitChoicesRespected()
    {
        Assert.AreEqual(WebGLExceptionSupport.FullWithoutStacktrace, AITBuildInitializer.ResolveExceptionSupport(-1, null));
        Assert.AreEqual(WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly, AITBuildInitializer.ResolveExceptionSupport(1, null));
        Assert.AreEqual(WebGLExceptionSupport.None, AITBuildInitializer.ResolveExceptionSupport(0, null));
        Assert.AreEqual(WebGLExceptionSupport.FullWithStacktrace, AITBuildInitializer.ResolveExceptionSupport(2, null));
    }

    [TestCase(WebGLExceptionSupport.None)]
    [TestCase(WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly)]
    public void WarnIfExceptionSupportUnsafe_UnsafeModes_LogWarning(WebGLExceptionSupport mode)
    {
        UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("FullWithoutStacktrace"));
        Assert.IsTrue(AITBuildInitializer.WarnIfExceptionSupportUnsafe(mode));
    }

    [TestCase(WebGLExceptionSupport.FullWithoutStacktrace)]
    [TestCase(WebGLExceptionSupport.FullWithStacktrace)]
    public void WarnIfExceptionSupportUnsafe_SafeModes_NoWarning(WebGLExceptionSupport mode)
    {
        Assert.IsFalse(AITBuildInitializer.WarnIfExceptionSupportUnsafe(mode));
        UnityEngine.TestTools.LogAssert.NoUnexpectedReceived();
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
