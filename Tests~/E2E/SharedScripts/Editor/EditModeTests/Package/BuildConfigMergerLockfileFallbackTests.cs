// -----------------------------------------------------------------------
// BuildConfigMergerLockfileFallbackTests.cs - CopyPnpmLockfileWithFallback 통합 검증
// Level 0: 임시 디렉토리(project/sdk/dest)로 Fix A 폴백 동작 검증
// -----------------------------------------------------------------------

using System.IO;
using NUnit.Framework;

namespace AppsInToss.Editor.Package.Tests
{
    [TestFixture]
    public class BuildConfigMergerLockfileFallbackTests
    {
        private string _projectDir;
        private string _sdkDir;
        private string _destDir;

        [SetUp]
        public void SetupTempDirs()
        {
            string root = Path.Combine(Path.GetTempPath(), "ait-lockfile-fallback-tests-" + System.Guid.NewGuid().ToString("N"));
            _projectDir = Path.Combine(root, "project");
            _sdkDir = Path.Combine(root, "sdk");
            _destDir = Path.Combine(root, "dest");
            Directory.CreateDirectory(_projectDir);
            Directory.CreateDirectory(_sdkDir);
            Directory.CreateDirectory(_destDir);
        }

        [TearDown]
        public void Cleanup()
        {
            string root = Path.GetDirectoryName(_projectDir);
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }

        private const string ProjectLockfileMarker = "PROJECT_LOCKFILE_MARKER";
        private const string SdkLockfileMarker = "SDK_LOCKFILE_MARKER";

        private static string MakePackageJson(string webFrameworkSpec)
        {
            return "{\n  \"dependencies\": {\n    \"@apps-in-toss/web-framework\": \"" + webFrameworkSpec + "\"\n  },\n  \"devDependencies\": {}\n}\n";
        }

        private static string MakeLockfile(string webFrameworkSpec, string marker)
        {
            return "lockfileVersion: '9.0'\n\n" +
                   "# " + marker + "\n\n" +
                   "settings:\n  autoInstallPeers: true\n\n" +
                   "importers:\n\n" +
                   "  .:\n" +
                   "    dependencies:\n" +
                   "      '@apps-in-toss/web-framework':\n" +
                   "        specifier: " + webFrameworkSpec + "\n" +
                   "        version: " + webFrameworkSpec + "\n";
        }

        [Test]
        public void CopyPnpmLockfileWithFallback_UsesProjectLockfile_WhenInSync()
        {
            File.WriteAllText(Path.Combine(_projectDir, "package.json"), MakePackageJson("2.4.7"));
            File.WriteAllText(Path.Combine(_projectDir, "pnpm-lock.yaml"), MakeLockfile("2.4.7", ProjectLockfileMarker));
            File.WriteAllText(Path.Combine(_sdkDir, "pnpm-lock.yaml"), MakeLockfile("2.4.7", SdkLockfileMarker));

            BuildConfigMerger.CopyPnpmLockfileWithFallback(_projectDir, _sdkDir, _destDir);

            string copied = File.ReadAllText(Path.Combine(_destDir, "pnpm-lock.yaml"));
            StringAssert.Contains(ProjectLockfileMarker, copied,
                "정합 상태일 때는 프로젝트 lockfile이 그대로 복사되어야 한다");
        }

        [Test]
        public void CopyPnpmLockfileWithFallback_FallsBackToSdk_WhenLockfileStale()
        {
            // Sentry B8 시나리오: 사용자 lockfile이 SDK 업그레이드 후 구 specifier를 보유.
            File.WriteAllText(Path.Combine(_projectDir, "package.json"), MakePackageJson("2.4.7"));
            File.WriteAllText(Path.Combine(_projectDir, "pnpm-lock.yaml"), MakeLockfile("2.4.1", ProjectLockfileMarker));
            File.WriteAllText(Path.Combine(_sdkDir, "pnpm-lock.yaml"), MakeLockfile("2.4.7", SdkLockfileMarker));

            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning,
                new System.Text.RegularExpressions.Regex("정합되지 않아 SDK lockfile로 폴백"));

            BuildConfigMerger.CopyPnpmLockfileWithFallback(_projectDir, _sdkDir, _destDir);

            string copied = File.ReadAllText(Path.Combine(_destDir, "pnpm-lock.yaml"));
            StringAssert.Contains(SdkLockfileMarker, copied,
                "stale 상태일 때는 SDK lockfile로 폴백해야 한다");
            Assert.IsFalse(copied.Contains(ProjectLockfileMarker),
                "stale 프로젝트 lockfile이 dest에 남아있으면 안 된다");
        }

        [Test]
        public void CopyPnpmLockfileWithFallback_FallsBackToSdk_WhenProjectPackageJsonMissing()
        {
            // package.json이 없으면 검증 불가 → 안전 폴백.
            File.WriteAllText(Path.Combine(_projectDir, "pnpm-lock.yaml"), MakeLockfile("2.4.7", ProjectLockfileMarker));
            File.WriteAllText(Path.Combine(_sdkDir, "pnpm-lock.yaml"), MakeLockfile("2.4.7", SdkLockfileMarker));

            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning,
                new System.Text.RegularExpressions.Regex("package.json이 없어 검증 불가"));

            BuildConfigMerger.CopyPnpmLockfileWithFallback(_projectDir, _sdkDir, _destDir);

            string copied = File.ReadAllText(Path.Combine(_destDir, "pnpm-lock.yaml"));
            StringAssert.Contains(SdkLockfileMarker, copied);
        }

        [Test]
        public void CopyPnpmLockfileWithFallback_UsesSdkLockfile_WhenProjectLockfileMissing()
        {
            // 프로젝트 lockfile이 없는 일반 케이스: 경고 없이 SDK 사용.
            File.WriteAllText(Path.Combine(_projectDir, "package.json"), MakePackageJson("2.4.7"));
            File.WriteAllText(Path.Combine(_sdkDir, "pnpm-lock.yaml"), MakeLockfile("2.4.7", SdkLockfileMarker));

            BuildConfigMerger.CopyPnpmLockfileWithFallback(_projectDir, _sdkDir, _destDir);

            string copied = File.ReadAllText(Path.Combine(_destDir, "pnpm-lock.yaml"));
            StringAssert.Contains(SdkLockfileMarker, copied);
        }

        [Test]
        public void CopyPnpmLockfileWithFallback_NoOp_WhenBothMissing()
        {
            // 양쪽 모두 없으면 dest에 lockfile이 생성되지 않아야 한다 (회귀 보호).
            BuildConfigMerger.CopyPnpmLockfileWithFallback(_projectDir, _sdkDir, _destDir);

            Assert.IsFalse(File.Exists(Path.Combine(_destDir, "pnpm-lock.yaml")),
                "양쪽 lockfile이 모두 없을 때 dest에 lockfile이 생성되면 안 된다");
        }
    }
}
