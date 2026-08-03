// -----------------------------------------------------------------------
// BuildConfigMergerJsonTests.cs - MergePackageJson / MergeDependencies 단위 테스트
// Level 0: 임시 파일 fixture로 JSON 병합 로직 검증
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace AppsInToss.Editor.Package.Tests
{
    [TestFixture]
    public class BuildConfigMergerJsonTests
    {
        private string _projectDir;
        private string _sdkDir;
        private string _destDir;

        [SetUp]
        public void SetupTempDirs()
        {
            string root = Path.Combine(Path.GetTempPath(), "ait-bcm-tests-" + System.Guid.NewGuid().ToString("N"));
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

        [Test]
        public void MergeDependencies_SdkOverridesProject_WhenSameKey()
        {
            var project = new Dictionary<string, object> { { "pkg", "1.0.0" } };
            var sdk = new Dictionary<string, object> { { "pkg", "2.0.0" } };

            var result = BuildConfigMerger.MergeDependencies(project, sdk);

            Assert.AreEqual("2.0.0", result["pkg"]);
        }

        [Test]
        public void MergeDependencies_KeepsProjectOnlyKeys()
        {
            var project = new Dictionary<string, object> { { "project-only", "1.0.0" } };
            var sdk = new Dictionary<string, object> { { "sdk-only", "2.0.0" } };

            var result = BuildConfigMerger.MergeDependencies(project, sdk);

            Assert.AreEqual("1.0.0", result["project-only"]);
            Assert.AreEqual("2.0.0", result["sdk-only"]);
        }

        [Test]
        public void MergeDependencies_HandlesNullProject()
        {
            var sdk = new Dictionary<string, object> { { "pkg", "2.0.0" } };

            var result = BuildConfigMerger.MergeDependencies(null, sdk);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("2.0.0", result["pkg"]);
        }

        [Test]
        public void MergeDependencies_HandlesNullSdk()
        {
            var project = new Dictionary<string, object> { { "pkg", "1.0.0" } };

            var result = BuildConfigMerger.MergeDependencies(project, null);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("1.0.0", result["pkg"]);
        }

        [Test]
        public void MergeDependencies_HandlesBothNull()
        {
            var result = BuildConfigMerger.MergeDependencies(null, null);

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void MergePackageJson_CopiesSdk_WhenProjectFileMissing()
        {
            File.WriteAllText(Path.Combine(_sdkDir, "package.json"),
                "{\"name\":\"sdk-default\",\"dependencies\":{\"a\":\"1\"}}");

            BuildConfigMerger.MergePackageJson(_projectDir, _sdkDir, _destDir);

            string merged = File.ReadAllText(Path.Combine(_destDir, "package.json"));
            StringAssert.Contains("sdk-default", merged);
        }

        [Test]
        public void MergePackageJson_MergesDependencies_WhenBothExist()
        {
            File.WriteAllText(Path.Combine(_projectDir, "package.json"),
                "{\"dependencies\":{\"user-pkg\":\"1.0.0\"}}");
            File.WriteAllText(Path.Combine(_sdkDir, "package.json"),
                "{\"name\":\"sdk\",\"dependencies\":{\"@apps-in-toss/web-framework\":\"2.4.7\"}}");

            BuildConfigMerger.MergePackageJson(_projectDir, _sdkDir, _destDir);

            string merged = File.ReadAllText(Path.Combine(_destDir, "package.json"));
            StringAssert.Contains("user-pkg", merged);
            StringAssert.Contains("@apps-in-toss/web-framework", merged);
        }

        [Test]
        public void MergePackageJson_CopiesSdk_WhenProjectJsonMalformed()
        {
            File.WriteAllText(Path.Combine(_projectDir, "package.json"), "{not valid json");
            File.WriteAllText(Path.Combine(_sdkDir, "package.json"),
                "{\"name\":\"sdk-fallback\"}");

            BuildConfigMerger.MergePackageJson(_projectDir, _sdkDir, _destDir);

            string merged = File.ReadAllText(Path.Combine(_destDir, "package.json"));
            StringAssert.Contains("sdk-fallback", merged);
        }

        [Test]
        public void MergePackageJson_SdkVersionWins_WhenSameDependencyKey()
        {
            File.WriteAllText(Path.Combine(_projectDir, "package.json"),
                "{\"dependencies\":{\"shared\":\"1.0.0\"}}");
            File.WriteAllText(Path.Combine(_sdkDir, "package.json"),
                "{\"name\":\"sdk\",\"dependencies\":{\"shared\":\"2.0.0\"}}");

            BuildConfigMerger.MergePackageJson(_projectDir, _sdkDir, _destDir);

            string merged = File.ReadAllText(Path.Combine(_destDir, "package.json"));
            StringAssert.Contains("\"2.0.0\"", merged);
            Assert.IsFalse(merged.Contains("\"1.0.0\""), "SDK 버전이 우선해야 하는데 프로젝트 버전이 남아있음");
        }

        [Test]
        public void MergePackageJson_PreservesSdkPackageManagerField()
        {
            // SDK의 top-level packageManager 필드가 머지 결과에 보존되는지 검증.
            // Fix C 회귀 보호: ait-build/package.json에 pnpm 버전 핀이 들어가야 한다.
            File.WriteAllText(Path.Combine(_projectDir, "package.json"),
                "{\"dependencies\":{\"user-pkg\":\"1.0.0\"}}");
            File.WriteAllText(Path.Combine(_sdkDir, "package.json"),
                "{\"name\":\"sdk\",\"packageManager\":\"pnpm@10.28.0\",\"dependencies\":{}}");

            BuildConfigMerger.MergePackageJson(_projectDir, _sdkDir, _destDir);

            string merged = File.ReadAllText(Path.Combine(_destDir, "package.json"));
            StringAssert.Contains("packageManager", merged);
            StringAssert.Contains("pnpm@10.28.0", merged);
        }
    }
}
