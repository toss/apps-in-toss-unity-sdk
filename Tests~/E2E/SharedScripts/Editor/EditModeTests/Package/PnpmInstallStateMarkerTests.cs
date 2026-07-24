// -----------------------------------------------------------------------
// PnpmInstallStateMarkerTests.cs - install 스킵 판정 마커 단위 테스트
// Level 0: 임시 디렉토리 fixture로 판정 로직만 확인 (pnpm 실행 없음)
// 판정이 잘못되면 (a) 불필요한 재설치(원래 문제 재발) 또는 (b) 오염된
// node_modules로 빌드(더 나쁨) — fail-closed 동작을 집중 검증한다.
// -----------------------------------------------------------------------

using System.IO;
using NUnit.Framework;

namespace AppsInToss.Editor.Package.Tests
{
    [TestFixture]
    public class PnpmInstallStateMarkerTests
    {
        private const string WebFrameworkVersion = "2.4.7";

        private string _tempDir;

        [SetUp]
        public void CreateTempDir()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "ait-marker-tests-" + System.Guid.NewGuid().ToString("N"));
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

        /// <summary>
        /// 스킵 가능한 정상 상태 fixture:
        /// package.json + pnpm-lock.yaml + node_modules/.pnpm/web-framework@버전 + 성공 마커
        /// </summary>
        private void CreateValidInstalledState()
        {
            File.WriteAllText(Path.Combine(_tempDir, "package.json"),
                "{\"dependencies\":{\"@apps-in-toss/web-framework\":\"" + WebFrameworkVersion + "\"}}");
            File.WriteAllText(Path.Combine(_tempDir, "pnpm-lock.yaml"), "lockfileVersion: '9.0'\n");
            Directory.CreateDirectory(Path.Combine(
                _tempDir, "node_modules", ".pnpm", "@apps-in-toss+web-framework@" + WebFrameworkVersion));
            PnpmInstallStateMarker.WriteMarkerAfterSuccessfulInstall(_tempDir);
        }

        // =====================================================
        // 스킵 성공 (전 조건 만족)
        // =====================================================

        [Test]
        public void WriteMarker_ThenShouldSkipInstall_RoundTrips()
        {
            CreateValidInstalledState();

            bool skip = PnpmInstallStateMarker.ShouldSkipInstall(_tempDir, out string reason);

            Assert.IsTrue(skip);
            Assert.IsNotNull(reason);
        }

        // =====================================================
        // fail-closed: 조건 하나라도 어긋나면 스킵 불가
        // =====================================================

        [Test]
        public void ShouldSkipInstall_ReturnsFalse_WhenMarkerMissing()
        {
            CreateValidInstalledState();
            File.Delete(PnpmInstallStateMarker.GetMarkerPath(_tempDir));

            Assert.IsFalse(PnpmInstallStateMarker.ShouldSkipInstall(_tempDir, out _));
        }

        [Test]
        public void ShouldSkipInstall_ReturnsFalse_WhenNodeModulesMissing()
        {
            CreateValidInstalledState();
            Directory.Delete(Path.Combine(_tempDir, "node_modules"), recursive: true);

            Assert.IsFalse(PnpmInstallStateMarker.ShouldSkipInstall(_tempDir, out _));
        }

        [Test]
        public void ShouldSkipInstall_ReturnsFalse_WhenPackageJsonChanged()
        {
            CreateValidInstalledState();
            File.WriteAllText(Path.Combine(_tempDir, "package.json"),
                "{\"dependencies\":{\"@apps-in-toss/web-framework\":\"9.9.9\"}}");

            Assert.IsFalse(PnpmInstallStateMarker.ShouldSkipInstall(_tempDir, out _));
        }

        [Test]
        public void ShouldSkipInstall_ReturnsFalse_WhenLockfileChanged()
        {
            CreateValidInstalledState();
            File.AppendAllText(Path.Combine(_tempDir, "pnpm-lock.yaml"), "# changed\n");

            Assert.IsFalse(PnpmInstallStateMarker.ShouldSkipInstall(_tempDir, out _));
        }

        [Test]
        public void ShouldSkipInstall_ReturnsFalse_WhenLockfileMissing()
        {
            CreateValidInstalledState();
            File.Delete(Path.Combine(_tempDir, "pnpm-lock.yaml"));

            Assert.IsFalse(PnpmInstallStateMarker.ShouldSkipInstall(_tempDir, out _));
        }

        [Test]
        public void ShouldSkipInstall_ReturnsFalse_WhenPnpmVersionMismatch()
        {
            CreateValidInstalledState();
            string markerPath = PnpmInstallStateMarker.GetMarkerPath(_tempDir);
            string marker = File.ReadAllText(markerPath);
            File.WriteAllText(markerPath,
                marker.Replace(AITPackageManagerHelper.PNPM_VERSION, "0.0.1"));

            Assert.IsFalse(PnpmInstallStateMarker.ShouldSkipInstall(_tempDir, out _));
        }

        [Test]
        public void ShouldSkipInstall_ReturnsFalse_WhenSchemaVersionMismatch()
        {
            // 해시/버전은 전부 유효하고 schemaVersion만 미래 값인 마커 — 직렬화 형식 의존 없이 직접 구성
            CreateValidInstalledState();
            var marker = new System.Collections.Generic.Dictionary<string, object>
            {
                { "schemaVersion", PnpmInstallStateMarker.SchemaVersion + 998 },
                { "pnpmVersion", AITPackageManagerHelper.PNPM_VERSION },
                { "packageJsonHash", PnpmInstallStateMarker.ComputeFileHash(Path.Combine(_tempDir, "package.json")) },
                { "lockfileHash", PnpmInstallStateMarker.ComputeFileHash(Path.Combine(_tempDir, "pnpm-lock.yaml")) },
            };
            File.WriteAllText(PnpmInstallStateMarker.GetMarkerPath(_tempDir), MiniJson.Serialize(marker));

            Assert.IsFalse(PnpmInstallStateMarker.ShouldSkipInstall(_tempDir, out _));
        }

        [Test]
        public void ShouldSkipInstall_ReturnsFalse_WhenMarkerJsonCorrupted()
        {
            CreateValidInstalledState();
            File.WriteAllText(PnpmInstallStateMarker.GetMarkerPath(_tempDir), "{not-valid-json!!");

            Assert.IsFalse(PnpmInstallStateMarker.ShouldSkipInstall(_tempDir, out _));
        }

        [Test]
        public void ShouldSkipInstall_ReturnsFalse_WhenNodeModulesIntegrityBroken()
        {
            // 해시/마커는 유효하지만 .pnpm 디렉토리가 사라진 오염 상태 (수동 삭제, 백신 격리 등)
            // → NodeModulesValidator 이중 게이트가 스킵을 막아야 함
            CreateValidInstalledState();
            Directory.Delete(Path.Combine(_tempDir, "node_modules", ".pnpm"), recursive: true);

            Assert.IsFalse(PnpmInstallStateMarker.ShouldSkipInstall(_tempDir, out _));
        }

        [Test]
        public void ShouldSkipInstall_ReturnsFalse_WhenKillSwitchEnvVarSet()
        {
            CreateValidInstalledState();
            System.Environment.SetEnvironmentVariable(PnpmInstallStateMarker.KillSwitchEnvVar, "1");
            try
            {
                Assert.IsFalse(PnpmInstallStateMarker.ShouldSkipInstall(_tempDir, out _));
            }
            finally
            {
                System.Environment.SetEnvironmentVariable(PnpmInstallStateMarker.KillSwitchEnvVar, null);
            }
        }

        // =====================================================
        // 마커 수명주기: CleanNodeModules와의 정합
        // =====================================================

        [Test]
        public void CleanNodeModules_InvalidatesMarker()
        {
            // 마커가 node_modules 내부에 있어 clean 재시도 단계에서 자동으로 함께 무효화되어야 함
            CreateValidInstalledState();

            NodeModulesValidator.CleanNodeModules(_tempDir);

            Assert.IsFalse(File.Exists(PnpmInstallStateMarker.GetMarkerPath(_tempDir)));
            Assert.IsFalse(PnpmInstallStateMarker.ShouldSkipInstall(_tempDir, out _));
        }

        [Test]
        public void WriteMarker_DoesNothing_WhenNodeModulesMissing()
        {
            // install 성공 직후가 아닌 상태(node_modules 없음)에서는 마커를 만들지 않아야 함
            File.WriteAllText(Path.Combine(_tempDir, "package.json"), "{}");
            File.WriteAllText(Path.Combine(_tempDir, "pnpm-lock.yaml"), "lockfileVersion: '9.0'\n");

            PnpmInstallStateMarker.WriteMarkerAfterSuccessfulInstall(_tempDir);

            Assert.IsFalse(File.Exists(PnpmInstallStateMarker.GetMarkerPath(_tempDir)));
        }

        // =====================================================
        // 해시 형식
        // =====================================================

        [Test]
        public void ComputeFileHash_IsStable_AndPrefixed()
        {
            string file = Path.Combine(_tempDir, "sample.txt");
            File.WriteAllText(file, "content");

            string hash1 = PnpmInstallStateMarker.ComputeFileHash(file);
            string hash2 = PnpmInstallStateMarker.ComputeFileHash(file);

            Assert.AreEqual(hash1, hash2);
            StringAssert.StartsWith("sha256:", hash1);
            Assert.AreEqual("sha256:".Length + 64, hash1.Length);
        }

        // =====================================================
        // PnpmRunner 통합: 스킵 시 프로세스가 전혀 스폰되지 않아야 함
        // =====================================================

        [Test]
        public void RunPnpmInstallSync_Skips_WithoutSpawningProcess_WhenStateValid()
        {
            // PnpmPath에 존재하지 않는 경로를 넣었으므로, 스킵이 동작하지 않고 실제 install을
            // 시도했다면 SUCCEED가 나올 수 없다 — SUCCEED 반환 자체가 "프로세스 미스폰"의 증거.
            CreateValidInstalledState();

            var ctx = new AITPackageBuilder.PackageContext
            {
                BuildProjectPath = _tempDir,
                PnpmPath = Path.Combine(_tempDir, "nonexistent-pnpm-binary"),
                LocalCachePath = _tempDir,
            };

            var result = PnpmRunner.RunPnpmInstallSync(ctx);

            Assert.AreEqual(AITConvertCore.AITExportError.SUCCEED, result);
        }
    }
}
