// -----------------------------------------------------------------------
// BuildConfigMergerTsTests.cs - MergeTsConfig 단위 테스트
// Level 0: 임시 파일 fixture로 tsconfig.json 병합 로직 검증
// -----------------------------------------------------------------------

using System.IO;
using NUnit.Framework;

namespace AppsInToss.Editor.Package.Tests
{
    [TestFixture]
    public class BuildConfigMergerTsTests
    {
        private string _projectDir;
        private string _sdkDir;
        private string _destDir;

        [SetUp]
        public void SetupTempDirs()
        {
            string root = Path.Combine(Path.GetTempPath(), "ait-bcm-ts-tests-" + System.Guid.NewGuid().ToString("N"));
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
        public void MergeTsConfig_CopiesSdk_WhenProjectFileMissing()
        {
            File.WriteAllText(Path.Combine(_sdkDir, "tsconfig.json"),
                "{\"compilerOptions\":{\"target\":\"ES2020\"}}");

            BuildConfigMerger.MergeTsConfig(_projectDir, _sdkDir, _destDir);

            string merged = File.ReadAllText(Path.Combine(_destDir, "tsconfig.json"));
            StringAssert.Contains("ES2020", merged);
        }

        [Test]
        public void MergeTsConfig_CopiesSdk_WhenProjectJsonMalformed()
        {
            File.WriteAllText(Path.Combine(_projectDir, "tsconfig.json"), "{bad json");
            File.WriteAllText(Path.Combine(_sdkDir, "tsconfig.json"),
                "{\"compilerOptions\":{\"target\":\"ES2020\"}}");

            BuildConfigMerger.MergeTsConfig(_projectDir, _sdkDir, _destDir);

            string merged = File.ReadAllText(Path.Combine(_destDir, "tsconfig.json"));
            StringAssert.Contains("ES2020", merged);
        }

        [Test]
        public void MergeTsConfig_WritesDestFile_WhenBothPresent()
        {
            File.WriteAllText(Path.Combine(_projectDir, "tsconfig.json"),
                "{\"include\":[\"src/**/*\"]}");
            File.WriteAllText(Path.Combine(_sdkDir, "tsconfig.json"),
                "{\"compilerOptions\":{\"target\":\"ES2020\"}}");

            BuildConfigMerger.MergeTsConfig(_projectDir, _sdkDir, _destDir);

            Assert.IsTrue(File.Exists(Path.Combine(_destDir, "tsconfig.json")));
        }

        [Test]
        public void MergeTsConfig_ForcesSdkValue_ForRequiredOption()
        {
            // 프로젝트가 SDK 필수 옵션(moduleResolution, esModuleInterop)을 덮어쓰려 시도
            File.WriteAllText(Path.Combine(_projectDir, "tsconfig.json"),
                "{\"compilerOptions\":{\"moduleResolution\":\"node\",\"esModuleInterop\":false}}");
            File.WriteAllText(Path.Combine(_sdkDir, "tsconfig.json"),
                "{\"compilerOptions\":{\"moduleResolution\":\"bundler\",\"esModuleInterop\":true}}");

            BuildConfigMerger.MergeTsConfig(_projectDir, _sdkDir, _destDir);

            string merged = File.ReadAllText(Path.Combine(_destDir, "tsconfig.json"));
            StringAssert.Contains("bundler", merged);
            Assert.IsFalse(merged.Contains("\"node\""), "moduleResolution은 SDK 필수 옵션이라 SDK 값이 강제되어야 함");
        }

        [Test]
        public void MergeTsConfig_PreservesProjectInclude()
        {
            File.WriteAllText(Path.Combine(_projectDir, "tsconfig.json"),
                "{\"include\":[\"custom/**\"]}");
            File.WriteAllText(Path.Combine(_sdkDir, "tsconfig.json"),
                "{\"compilerOptions\":{\"target\":\"ES2020\"},\"include\":[\"sdk-default/**\"]}");

            BuildConfigMerger.MergeTsConfig(_projectDir, _sdkDir, _destDir);

            string merged = File.ReadAllText(Path.Combine(_destDir, "tsconfig.json"));
            StringAssert.Contains("custom/**", merged);
            Assert.IsFalse(merged.Contains("sdk-default/**"), "프로젝트 include가 우선되어야 함");
        }
    }
}
