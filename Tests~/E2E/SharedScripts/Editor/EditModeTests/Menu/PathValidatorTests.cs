using System;
using System.IO;
using NUnit.Framework;
using AppsInToss.Editor.Menu;

namespace AppsInToss.Editor.Menu.Tests
{
    public class PathValidatorTests
    {
        private string tempDir;

        [SetUp]
        public void Setup()
        {
            tempDir = Path.Combine(Path.GetTempPath(), "ait-pathvalidator-test-" + Guid.NewGuid());
            Directory.CreateDirectory(tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }

        [Test]
        public void ValidateBuildPath_NonExistentPath_ReturnsFalse()
        {
            string nonExistent = Path.Combine(tempDir, "nonexistent");
            bool result = PathValidator.ValidateBuildPath(nonExistent);
            Assert.IsFalse(result);
        }

        [Test]
        public void ValidateBuildPath_ExistingDirectoryWithIndexHtml_ReturnsTrue()
        {
            File.WriteAllText(Path.Combine(tempDir, "index.html"), "<html></html>");
            bool result = PathValidator.ValidateBuildPath(tempDir);
            Assert.IsTrue(result);
        }

        [Test]
        public void ValidateBuildPath_ExistingDirectoryWithoutIndexHtml_ReturnsFalse()
        {
            bool result = PathValidator.ValidateBuildPath(tempDir);
            Assert.IsFalse(result);
        }

        [Test]
        public void IsErrorOutput_NormalString_ReturnsFalse()
        {
            Assert.IsFalse(PathValidator.IsErrorOutput("hello world"));
        }

        [Test]
        public void IsErrorOutput_EmptyOrNull_ReturnsFalse()
        {
            Assert.IsFalse(PathValidator.IsErrorOutput(""));
            Assert.IsFalse(PathValidator.IsErrorOutput(null));
        }

        [Test]
        public void IsErrorOutput_ErrorKeyword_ReturnsTrue()
        {
            // 실제 구현은 "error:", "fatal:", "eaddrinuse", "port is already in use",
            // "address already in use", "cannot find module", "command not found",
            // "permission denied", "build failed", "deploy failed", "install failed" 중 하나라도 포함 시 true
            Assert.IsTrue(PathValidator.IsErrorOutput("ERROR: something failed"));
            Assert.IsTrue(PathValidator.IsErrorOutput("Fatal: corruption detected"));
            Assert.IsTrue(PathValidator.IsErrorOutput("listen EADDRINUSE: port 5173"));
            Assert.IsTrue(PathValidator.IsErrorOutput("Build failed with errors"));
        }

        [Test]
        public void ValidateNpmPath_EmptyPath_ReturnsFalse()
        {
            Assert.IsFalse(PathValidator.ValidateNpmPath(""));
            Assert.IsFalse(PathValidator.ValidateNpmPath(null));
        }

        [Test]
        public void ValidateNpmPath_NonEmptyPath_ReturnsTrue()
        {
            Assert.IsTrue(PathValidator.ValidateNpmPath("/usr/local/bin/npm"));
        }
    }
}
