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
}
