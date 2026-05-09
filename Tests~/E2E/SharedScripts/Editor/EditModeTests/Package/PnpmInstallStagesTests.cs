// -----------------------------------------------------------------------
// PnpmInstallStagesTests.cs - InstallStages 정책 + DeleteLockfileIfExists IO 동작 검증
// Level 0: pnpm 실행 없이 정책 데이터와 파일 삭제 헬퍼만 검증
// -----------------------------------------------------------------------

using System.IO;
using NUnit.Framework;

namespace AppsInToss.Editor.Package.Tests
{
    /// <summary>
    /// PnpmRunner 메시지/상수 정책 회귀 테스트.
    /// Sentry SDK-R5 fingerprint와 일치하는 최종 실패 메시지가 변경되지 않도록 보장한다.
    /// </summary>
    [TestFixture]
    public class PnpmRunnerMessagesTests
    {
        [Test]
        public void FinalFailureMessage_MatchesSentrySdkR5Fingerprint()
        {
            // Sentry 이슈 SDK-R5는 정확히 이 메시지로 fingerprint되어 있으므로 변경 시 이슈가 분리된다.
            // 메시지를 바꾸려면 Sentry 측 fingerprint도 함께 갱신해야 한다.
            Assert.AreEqual("[AIT] pnpm install 실패 (모든 재시도 후에도 실패)", PnpmRunner.FinalFailureMessage);
        }

        [Test]
        public void FinalFailureMessage_IsClassifiedAsAitSdkSource()
        {
            // FinalFailureMessage는 [AIT] 토큰을 포함해 ErrorTracker가 SDK 출처로 분류해야 한다.
            // 분류기와 메시지가 어긋나면 진짜 실패가 Sentry에서 누락될 수 있다.
            StringAssert.Contains("[AIT]", PnpmRunner.FinalFailureMessage);
            StringAssert.Contains("pnpm install", PnpmRunner.FinalFailureMessage);
            StringAssert.Contains("모든 재시도 후에도 실패", PnpmRunner.FinalFailureMessage);
        }
    }

    [TestFixture]
    public class PnpmInstallStagesTests
    {
        private string _tempDir;

        [SetUp]
        public void CreateTempDir()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "ait-stages-tests-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        [TearDown]
        public void CleanupTempDir()
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }

        [Test]
        public void InstallStages_HasFourStages()
        {
            Assert.AreEqual(4, PnpmStoreManager.InstallStages.Count,
                "Fix B로 lockfile 폐기 단계가 추가되어 총 4단계여야 한다");
        }

        [Test]
        public void InstallStages_FirstStageIsFrozenLockfile()
        {
            var first = PnpmStoreManager.InstallStages[0];
            StringAssert.Contains("--frozen-lockfile", first.args);
            Assert.IsFalse(first.cleanFirst);
            Assert.IsFalse(first.deleteLockfileFirst);
        }

        [Test]
        public void InstallStages_LockfileDeleteStageExistsBeforeCleanRetry()
        {
            // Fix B 핵심 정책: lockfile 폐기 단계가 clean 재시도보다 먼저 와야 한다.
            // node_modules는 보존하면서 손상 lockfile만 폐기해 install 비용을 최소화한다.
            int deleteIdx = -1, cleanIdx = -1;
            for (int i = 0; i < PnpmStoreManager.InstallStages.Count; i++)
            {
                var stage = PnpmStoreManager.InstallStages[i];
                if (stage.deleteLockfileFirst) deleteIdx = i;
                if (stage.cleanFirst) cleanIdx = i;
            }

            Assert.GreaterOrEqual(deleteIdx, 0, "lockfile 폐기 단계가 정의되어 있어야 한다");
            Assert.GreaterOrEqual(cleanIdx, 0, "clean 재시도 단계가 정의되어 있어야 한다");
            Assert.Less(deleteIdx, cleanIdx, "lockfile 폐기 단계가 clean 재시도보다 먼저여야 한다");
        }

        [Test]
        public void InstallStages_NoStageHasBothCleanAndLockfileDelete()
        {
            // 두 플래그가 동시에 켜진 단계는 의미 중복 (clean이 lockfile도 결과적으로 무효화).
            foreach (var stage in PnpmStoreManager.InstallStages)
            {
                Assert.IsFalse(stage.cleanFirst && stage.deleteLockfileFirst,
                    $"단계 '{stage.label}'에서 cleanFirst와 deleteLockfileFirst가 동시에 true");
            }
        }

        [Test]
        public void DeleteLockfileIfExists_RemovesLockfile_WhenPresent()
        {
            string lockfilePath = Path.Combine(_tempDir, "pnpm-lock.yaml");
            File.WriteAllText(lockfilePath, "lockfileVersion: '9.0'\n");

            PnpmRunner.DeleteLockfileIfExists(_tempDir);

            Assert.IsFalse(File.Exists(lockfilePath), "lockfile이 삭제되어야 한다");
        }

        [Test]
        public void DeleteLockfileIfExists_NoOp_WhenLockfileMissing()
        {
            // 파일이 없을 때 예외를 던지지 않아야 한다.
            Assert.DoesNotThrow(() => PnpmRunner.DeleteLockfileIfExists(_tempDir));
        }

        [Test]
        public void DeleteLockfileIfExists_DoesNotTouchOtherFiles()
        {
            string lockfilePath = Path.Combine(_tempDir, "pnpm-lock.yaml");
            string packageJsonPath = Path.Combine(_tempDir, "package.json");
            File.WriteAllText(lockfilePath, "lockfileVersion: '9.0'\n");
            File.WriteAllText(packageJsonPath, "{\"name\":\"test\"}");

            PnpmRunner.DeleteLockfileIfExists(_tempDir);

            Assert.IsFalse(File.Exists(lockfilePath));
            Assert.IsTrue(File.Exists(packageJsonPath), "package.json은 보존되어야 한다");
        }
    }
}
