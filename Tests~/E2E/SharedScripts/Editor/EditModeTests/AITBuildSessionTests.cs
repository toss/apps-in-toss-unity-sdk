using System.Diagnostics;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using AppsInToss.Editor;

namespace AppsInToss.Editor.EditModeTests
{
    public class PlayerSettingsSnapshotTests
    {
        [Test]
        public void Capture_RoundTrips_WebGLCompressionFormat()
        {
            var originalCompression = PlayerSettings.WebGL.compressionFormat;
            try
            {
                PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
                var snapshot = PlayerSettingsSnapshot.Capture();

                PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
                Assert.AreNotEqual(WebGLCompressionFormat.Brotli, PlayerSettings.WebGL.compressionFormat);

                snapshot.Restore();
                Assert.AreEqual(WebGLCompressionFormat.Brotli, PlayerSettings.WebGL.compressionFormat);
            }
            finally
            {
                PlayerSettings.WebGL.compressionFormat = originalCompression;
            }
        }

        [Test]
        public void Capture_RoundTrips_SplashScreenSettings()
        {
            bool originalShow = PlayerSettings.SplashScreen.show;
            bool originalShowUnityLogo = PlayerSettings.SplashScreen.showUnityLogo;

            // Personal 라이선스 에디터에서는 SplashScreen.show=false 대입이 조용히 무시된다
            // (Plus/Pro 전용 기능). round-trip은 값 변경이 전제이므로 setter가 유효한 환경에서만 검증.
            PlayerSettings.SplashScreen.show = false;
            bool setterEffective = PlayerSettings.SplashScreen.show == false;
            PlayerSettings.SplashScreen.show = originalShow;
            if (!setterEffective)
            {
                Assert.Ignore("Personal 라이선스 에디터에서는 SplashScreen setter가 무시되어 round-trip을 검증할 수 없습니다.");
                return;
            }

            try
            {
                PlayerSettings.SplashScreen.show = true;
                PlayerSettings.SplashScreen.showUnityLogo = true;
                var snapshot = PlayerSettingsSnapshot.Capture();

                PlayerSettings.SplashScreen.show = false;
                PlayerSettings.SplashScreen.showUnityLogo = false;
                Assert.IsFalse(PlayerSettings.SplashScreen.show);
                Assert.IsFalse(PlayerSettings.SplashScreen.showUnityLogo);

                snapshot.Restore();
                Assert.IsTrue(PlayerSettings.SplashScreen.show,
                    "Restore must return SplashScreen.show to the captured pre-build value");
                Assert.IsTrue(PlayerSettings.SplashScreen.showUnityLogo,
                    "Restore must return SplashScreen.showUnityLogo to the captured pre-build value");
            }
            finally
            {
                PlayerSettings.SplashScreen.show = originalShow;
                PlayerSettings.SplashScreen.showUnityLogo = originalShowUnityLogo;
            }
        }

        [Test]
        public void IsSerializable_CanRoundTripThroughJsonUtility()
        {
            var snapshot = PlayerSettingsSnapshot.Capture();
            string json = JsonUtility.ToJson(snapshot);
            var restored = JsonUtility.FromJson<PlayerSettingsSnapshot>(json);
            Assert.AreEqual(snapshot.webGLTemplate, restored.webGLTemplate);
        }
    }

    public class AITBuildSessionTests
    {
        [SetUp]
        public void SetUp() => AITBuildSession.EndBuild();

        [TearDown]
        public void TearDown() => AITBuildSession.EndBuild();

        [Test]
        public void BeginBuild_SetsSessionIdAndStartTime()
        {
            AITBuildSession.BeginBuild("Test");
            Assert.IsTrue(AITBuildSession.TryLoadPendingSession(out var session));
            Assert.IsFalse(string.IsNullOrEmpty(session.sessionId));
            Assert.Greater(session.startedAtUnixSec, 0);
            Assert.AreEqual("Test", session.entrypoint);
            Assert.AreEqual(BuildStage.Preparing, session.stage);
        }

        [Test]
        public void SetStage_UpdatesStage()
        {
            AITBuildSession.BeginBuild("Test");
            AITBuildSession.SetStage(BuildStage.WebGLBuild);
            AITBuildSession.TryLoadPendingSession(out var session);
            Assert.AreEqual(BuildStage.WebGLBuild, session.stage);
        }

        [Test]
        public void RecordPid_AccumulatesMultiple()
        {
            AITBuildSession.BeginBuild("Test");
            AITBuildSession.RecordPid(1234);
            AITBuildSession.RecordPid(5678);
            AITBuildSession.TryLoadPendingSession(out var session);
            CollectionAssert.AreEquivalent(new[] { 1234, 5678 }, session.childPids);
        }

        [Test]
        public void ClearPid_RemovesMatchingOnly()
        {
            AITBuildSession.BeginBuild("Test");
            AITBuildSession.RecordPid(1234);
            AITBuildSession.RecordPid(5678);
            AITBuildSession.ClearPid(1234);
            AITBuildSession.TryLoadPendingSession(out var session);
            CollectionAssert.AreEquivalent(new[] { 5678 }, session.childPids);
        }

        [Test]
        public void EndBuild_ClearsStateAndHidesSession()
        {
            AITBuildSession.BeginBuild("Test");
            AITBuildSession.EndBuild();
            Assert.IsFalse(AITBuildSession.TryLoadPendingSession(out _));
        }

        [Test]
        public void TryLoadPendingSession_ReturnsFalseWhenNoActiveSession()
        {
            AITBuildSession.EndBuild();
            Assert.IsFalse(AITBuildSession.TryLoadPendingSession(out _));
        }

        [Test]
        public void IsStale_TrueWhenOlderThan24Hours()
        {
            AITBuildSession.BeginBuild("Test");
            AITBuildSession.TryLoadPendingSession(out var session);
            // Directly clobber the timestamp for the test; this is internal-test-only
            // and exercised because the production code cannot fabricate time.
            AITBuildSession.ForceTestOverride(startedAtUnixSec: session.startedAtUnixSec - 25 * 3600);
            AITBuildSession.TryLoadPendingSession(out var stale);
            Assert.IsTrue(AITBuildSession.IsStale(stale));
        }

        [Test]
        public void IsStale_TrueWhenUnityVersionMismatch()
        {
            AITBuildSession.BeginBuild("Test");
            AITBuildSession.ForceTestOverride(unityVersion: "9999.9.9f0");
            AITBuildSession.TryLoadPendingSession(out var stale);
            Assert.IsTrue(AITBuildSession.IsStale(stale));
        }

        [Test]
        public void IsStale_TrueWhenSdkVersionMismatch()
        {
            AITBuildSession.BeginBuild("Test");
            AITBuildSession.ForceTestOverride(sdkVersion: "0.0.0");
            AITBuildSession.TryLoadPendingSession(out var stale);
            Assert.IsTrue(AITBuildSession.IsStale(stale));
        }

        [Test]
        public void IsStale_FalseForFreshValidSession()
        {
            AITBuildSession.BeginBuild("Test");
            AITBuildSession.TryLoadPendingSession(out var fresh);
            Assert.IsFalse(AITBuildSession.IsStale(fresh));
        }
    }

    public class AITBuildSessionRecoveryTests
    {
        [Test]
        public void TryKillIfRunning_ReturnsFalseForNonexistentPid()
        {
            // 절대 존재하지 않는 PID — Process.GetProcessById 가 ArgumentException 을 던지는 경로.
            Assert.DoesNotThrow(() =>
            {
                bool killed = AITBuildSessionRecovery.TryKillIfRunning(int.MaxValue);
                Assert.IsFalse(killed);
            });
        }

        [Test]
        public void TryKillIfRunning_DoesNotThrowForAlreadyExitedProcess()
        {
            // 즉시 종료되는 자식 프로세스를 spawn — Sentry APPS-IN-TOSS-UNITY-SDK-NT 의
            // race window (GetProcessById 성공, Kill 시점엔 exited) 를 재현하기 위함.
            // OS 스케줄링에 따라 ArgumentException 또는 InvalidOperationException 이 발생할 수
            // 있으나, 두 경로 모두 silent 하게 false 를 리턴해야 한다.
            int pid;
#if UNITY_EDITOR_WIN
            var psi = new ProcessStartInfo("cmd.exe", "/c exit 0")
            {
                UseShellExecute = false,
                CreateNoWindow = true
            };
#else
            var psi = new ProcessStartInfo("/bin/sh", "-c \"exit 0\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true
            };
#endif
            using (var p = Process.Start(psi))
            {
                Assert.IsNotNull(p);
                pid = p.Id;
                p.WaitForExit(5000);
                Assert.IsTrue(p.HasExited, "테스트 셋업: 자식 프로세스가 종료되어야 한다.");
            }

            Assert.DoesNotThrow(() =>
            {
                bool killed = AITBuildSessionRecovery.TryKillIfRunning(pid);
                // 이미 종료된 PID 는 어느 catch 경로(ArgumentException/InvalidOperationException/
                // Win32Exception)로 떨어지든 silent false 가 계약.
                Assert.IsFalse(killed);
            });
        }
    }
}
