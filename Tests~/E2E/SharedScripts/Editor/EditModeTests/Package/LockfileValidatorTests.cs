// -----------------------------------------------------------------------
// LockfileValidatorTests.cs - 사용자 BuildConfig~ 의 lockfile/package.json 정합성 검증
// Level 0: 임시 파일 fixture로 정합성 판정 로직 검증
// -----------------------------------------------------------------------

using System.IO;
using NUnit.Framework;

namespace AppsInToss.Editor.Package.Tests
{
    [TestFixture]
    public class LockfileValidatorTests
    {
        private string _dir;
        private string _packageJsonPath;
        private string _lockfilePath;

        [SetUp]
        public void SetupTempDir()
        {
            _dir = Path.Combine(Path.GetTempPath(), "ait-lockfile-tests-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _packageJsonPath = Path.Combine(_dir, "package.json");
            _lockfilePath = Path.Combine(_dir, "pnpm-lock.yaml");
        }

        [TearDown]
        public void Cleanup()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
        }

        private static string MakePackageJson(string deps, string devDeps = "")
        {
            return "{\n" +
                   "  \"name\": \"test\",\n" +
                   "  \"dependencies\": {" + deps + "},\n" +
                   "  \"devDependencies\": {" + devDeps + "}\n" +
                   "}\n";
        }

        private static string MakeLockfile(string lockfileVersion, string depsBlock, string devDepsBlock = "")
        {
            return $"lockfileVersion: '{lockfileVersion}'\n\n" +
                   "settings:\n" +
                   "  autoInstallPeers: true\n\n" +
                   "importers:\n\n" +
                   "  .:\n" +
                   "    dependencies:\n" + depsBlock +
                   (string.IsNullOrEmpty(devDepsBlock) ? "" : "    devDependencies:\n" + devDepsBlock);
        }

        [Test]
        public void IsLockfileInSync_AllSpecifiersMatch_ReturnsTrue()
        {
            File.WriteAllText(_packageJsonPath,
                MakePackageJson("\"@apps-in-toss/web-framework\": \"2.4.7\""));
            File.WriteAllText(_lockfilePath, MakeLockfile("9.0",
                "      '@apps-in-toss/web-framework':\n" +
                "        specifier: 2.4.7\n" +
                "        version: 2.4.7\n"));

            bool result = LockfileValidator.IsLockfileInSync(_packageJsonPath, _lockfilePath, out string summary);

            Assert.IsTrue(result, "All specifiers match should return true. Summary: " + summary);
            Assert.IsEmpty(summary);
        }

        [Test]
        public void IsLockfileInSync_StaleSpecifier_ReturnsFalse()
        {
            File.WriteAllText(_packageJsonPath,
                MakePackageJson("\"@apps-in-toss/web-framework\": \"2.4.7\""));
            File.WriteAllText(_lockfilePath, MakeLockfile("9.0",
                "      '@apps-in-toss/web-framework':\n" +
                "        specifier: 2.4.1\n" +
                "        version: 2.4.1\n"));

            bool result = LockfileValidator.IsLockfileInSync(_packageJsonPath, _lockfilePath, out string summary);

            Assert.IsFalse(result);
            StringAssert.Contains("@apps-in-toss/web-framework", summary);
            StringAssert.Contains("2.4.1", summary);
            StringAssert.Contains("2.4.7", summary);
        }

        [Test]
        public void IsLockfileInSync_DepMissingFromLockfile_ReturnsFalse()
        {
            File.WriteAllText(_packageJsonPath,
                MakePackageJson(
                    "\"@apps-in-toss/web-framework\": \"2.4.7\"," +
                    "\"new-dep\": \"1.0.0\""));
            File.WriteAllText(_lockfilePath, MakeLockfile("9.0",
                "      '@apps-in-toss/web-framework':\n" +
                "        specifier: 2.4.7\n" +
                "        version: 2.4.7\n"));

            bool result = LockfileValidator.IsLockfileInSync(_packageJsonPath, _lockfilePath, out string summary);

            Assert.IsFalse(result);
            StringAssert.Contains("new-dep", summary);
        }

        [Test]
        public void IsLockfileInSync_DevDependenciesAlsoChecked()
        {
            File.WriteAllText(_packageJsonPath,
                MakePackageJson(
                    "\"@apps-in-toss/web-framework\": \"2.4.7\"",
                    "\"vite\": \"8.0.10\""));
            File.WriteAllText(_lockfilePath, MakeLockfile("9.0",
                "      '@apps-in-toss/web-framework':\n" +
                "        specifier: 2.4.7\n" +
                "        version: 2.4.7\n",
                "      vite:\n" +
                "        specifier: 8.0.8\n" +
                "        version: 8.0.8\n"));

            bool result = LockfileValidator.IsLockfileInSync(_packageJsonPath, _lockfilePath, out string summary);

            Assert.IsFalse(result);
            StringAssert.Contains("vite", summary);
        }

        [Test]
        public void IsLockfileInSync_UnknownLockfileVersion_ReturnsFalse()
        {
            File.WriteAllText(_packageJsonPath,
                MakePackageJson("\"@apps-in-toss/web-framework\": \"2.4.7\""));
            File.WriteAllText(_lockfilePath, MakeLockfile("99.0",
                "      '@apps-in-toss/web-framework':\n" +
                "        specifier: 2.4.7\n" +
                "        version: 2.4.7\n"));

            bool result = LockfileValidator.IsLockfileInSync(_packageJsonPath, _lockfilePath, out string summary);

            Assert.IsFalse(result);
            StringAssert.Contains("lockfileVersion", summary);
        }

        [Test]
        public void IsLockfileInSync_PackageJsonMissing_ReturnsFalse()
        {
            File.WriteAllText(_lockfilePath, MakeLockfile("9.0",
                "      '@apps-in-toss/web-framework':\n" +
                "        specifier: 2.4.7\n" +
                "        version: 2.4.7\n"));

            bool result = LockfileValidator.IsLockfileInSync(_packageJsonPath, _lockfilePath, out string summary);

            Assert.IsFalse(result);
        }

        [Test]
        public void IsLockfileInSync_LockfileMissing_ReturnsFalse()
        {
            File.WriteAllText(_packageJsonPath,
                MakePackageJson("\"@apps-in-toss/web-framework\": \"2.4.7\""));

            bool result = LockfileValidator.IsLockfileInSync(_packageJsonPath, _lockfilePath, out string summary);

            Assert.IsFalse(result);
        }
    }
}
