// -----------------------------------------------------------------------
// NodeModulesValidatorTests.cs - NodeModulesValidator 단위 테스트
// Level 0: 임시 디렉토리 fixture로 검증 로직만 확인 (pnpm 실행 없음)
// -----------------------------------------------------------------------

using System.IO;
using NUnit.Framework;

namespace AppsInToss.Editor.Package.Tests
{
    [TestFixture]
    public class NodeModulesValidatorTests
    {
        private string _tempDir;

        [SetUp]
        public void CreateTempDir()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "ait-nm-tests-" + System.Guid.NewGuid().ToString("N"));
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
        public void ValidateIntegrity_ReturnsTrue_WhenNodeModulesMissing()
        {
            // node_modules가 없으면 install로 새로 생성될 것이므로 검증 불필요 → true
            Assert.IsTrue(NodeModulesValidator.ValidateIntegrity(_tempDir));
        }

        [Test]
        public void ValidateIntegrity_ReturnsTrue_WhenPackageJsonMissing()
        {
            Directory.CreateDirectory(Path.Combine(_tempDir, "node_modules"));
            Assert.IsTrue(NodeModulesValidator.ValidateIntegrity(_tempDir));
        }

        [Test]
        public void ValidateIntegrity_ReturnsTrue_WhenWebFrameworkNotInDependencies()
        {
            Directory.CreateDirectory(Path.Combine(_tempDir, "node_modules"));
            File.WriteAllText(Path.Combine(_tempDir, "package.json"),
                "{\"dependencies\":{\"other-pkg\":\"1.0.0\"}}");
            Assert.IsTrue(NodeModulesValidator.ValidateIntegrity(_tempDir));
        }

        [Test]
        public void ValidateIntegrity_ReturnsFalse_WhenPnpmDirMissing()
        {
            // node_modules는 있는데 .pnpm 디렉토리가 없으면 오염된 상태
            Directory.CreateDirectory(Path.Combine(_tempDir, "node_modules"));
            File.WriteAllText(Path.Combine(_tempDir, "package.json"),
                "{\"dependencies\":{\"@apps-in-toss/web-framework\":\"2.4.7\"}}");
            Assert.IsFalse(NodeModulesValidator.ValidateIntegrity(_tempDir));
        }

        [Test]
        public void ValidateIntegrity_ReturnsTrue_WhenVersionMatches()
        {
            string pnpmDir = Path.Combine(_tempDir, "node_modules", ".pnpm");
            Directory.CreateDirectory(pnpmDir);
            Directory.CreateDirectory(Path.Combine(pnpmDir, "@apps-in-toss+web-framework@2.4.7"));
            File.WriteAllText(Path.Combine(_tempDir, "package.json"),
                "{\"dependencies\":{\"@apps-in-toss/web-framework\":\"2.4.7\"}}");
            Assert.IsTrue(NodeModulesValidator.ValidateIntegrity(_tempDir));
        }

        [Test]
        public void ValidateIntegrity_StripsCaretPrefix_WhenComparingVersions()
        {
            // package.json에 "^2.4.7"로 적혀 있어도 설치 디렉토리가 2.4.7이면 matching
            string pnpmDir = Path.Combine(_tempDir, "node_modules", ".pnpm");
            Directory.CreateDirectory(pnpmDir);
            Directory.CreateDirectory(Path.Combine(pnpmDir, "@apps-in-toss+web-framework@2.4.7"));
            File.WriteAllText(Path.Combine(_tempDir, "package.json"),
                "{\"dependencies\":{\"@apps-in-toss/web-framework\":\"^2.4.7\"}}");
            Assert.IsTrue(NodeModulesValidator.ValidateIntegrity(_tempDir));
        }

        [Test]
        public void ValidateIntegrity_StripsTildePrefix_WhenComparingVersions()
        {
            string pnpmDir = Path.Combine(_tempDir, "node_modules", ".pnpm");
            Directory.CreateDirectory(pnpmDir);
            Directory.CreateDirectory(Path.Combine(pnpmDir, "@apps-in-toss+web-framework@2.4.7"));
            File.WriteAllText(Path.Combine(_tempDir, "package.json"),
                "{\"dependencies\":{\"@apps-in-toss/web-framework\":\"~2.4.7\"}}");
            Assert.IsTrue(NodeModulesValidator.ValidateIntegrity(_tempDir));
        }

        [Test]
        public void ValidateIntegrity_ReturnsTrue_WhenPackageJsonIsMalformed()
        {
            // 파싱 실패/부분 파싱/예외 어느 경로든 best-effort 계약에 따라 true를 반환해야 한다.
            Directory.CreateDirectory(Path.Combine(_tempDir, "node_modules"));
            File.WriteAllText(Path.Combine(_tempDir, "package.json"), "{ this is not json ");
            Assert.IsTrue(NodeModulesValidator.ValidateIntegrity(_tempDir));
        }

        [Test]
        public void ValidateIntegrity_ReturnsTrue_WhenDependenciesKeyMissing()
        {
            Directory.CreateDirectory(Path.Combine(_tempDir, "node_modules"));
            File.WriteAllText(Path.Combine(_tempDir, "package.json"), "{\"name\":\"x\"}");
            Assert.IsTrue(NodeModulesValidator.ValidateIntegrity(_tempDir));
        }

        [Test]
        public void ValidateIntegrity_ReturnsTrue_WhenVersionStringEmpty()
        {
            Directory.CreateDirectory(Path.Combine(_tempDir, "node_modules"));
            File.WriteAllText(Path.Combine(_tempDir, "package.json"),
                "{\"dependencies\":{\"@apps-in-toss/web-framework\":\"\"}}");
            Assert.IsTrue(NodeModulesValidator.ValidateIntegrity(_tempDir));
        }

        [Test]
        public void ValidateIntegrity_ReturnsFalse_WhenPnpmDirHasOtherPackagesOnly()
        {
            // .pnpm이 존재하지만 web-framework는 없음 → 정리 필요
            string pnpmDir = Path.Combine(_tempDir, "node_modules", ".pnpm");
            Directory.CreateDirectory(pnpmDir);
            Directory.CreateDirectory(Path.Combine(pnpmDir, "some-other-pkg@1.0.0"));
            File.WriteAllText(Path.Combine(_tempDir, "package.json"),
                "{\"dependencies\":{\"@apps-in-toss/web-framework\":\"2.4.7\"}}");
            Assert.IsFalse(NodeModulesValidator.ValidateIntegrity(_tempDir));
        }

        [Test]
        public void ValidateIntegrity_ReturnsFalse_WhenVersionMismatch()
        {
            string pnpmDir = Path.Combine(_tempDir, "node_modules", ".pnpm");
            Directory.CreateDirectory(pnpmDir);
            Directory.CreateDirectory(Path.Combine(pnpmDir, "@apps-in-toss+web-framework@2.4.6"));
            File.WriteAllText(Path.Combine(_tempDir, "package.json"),
                "{\"dependencies\":{\"@apps-in-toss/web-framework\":\"2.4.7\"}}");
            Assert.IsFalse(NodeModulesValidator.ValidateIntegrity(_tempDir));
        }

        [Test]
        public void CleanNodeModules_RemovesNodeModulesAndLegacyCache()
        {
            string nodeModules = Path.Combine(_tempDir, "node_modules");
            string legacyCache = Path.Combine(_tempDir, ".npm-cache");
            Directory.CreateDirectory(nodeModules);
            Directory.CreateDirectory(legacyCache);
            File.WriteAllText(Path.Combine(nodeModules, "dummy.txt"), "x");

            NodeModulesValidator.CleanNodeModules(_tempDir);

            Assert.IsFalse(Directory.Exists(nodeModules), "node_modules가 삭제되어야 함");
            Assert.IsFalse(Directory.Exists(legacyCache), ".npm-cache가 삭제되어야 함");
        }

        [Test]
        public void CleanNodeModules_DoesNotThrow_WhenDirectoriesMissing()
        {
            Assert.DoesNotThrow(() => NodeModulesValidator.CleanNodeModules(_tempDir));
        }
    }
}
