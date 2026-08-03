using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using AppsInToss;

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
        public void ValidateNpmPath_EmptyOrNullPath_ReturnsFalse()
        {
            // AITLog.Error → Debug.LogError 를 호출하므로 LogAssert.Expect 필요
            LogAssert.Expect(LogType.Error, "AIT: npm을 찾을 수 없습니다.");
            Assert.IsFalse(PathValidator.ValidateNpmPath(""));

            LogAssert.Expect(LogType.Error, "AIT: npm을 찾을 수 없습니다.");
            Assert.IsFalse(PathValidator.ValidateNpmPath(null));
        }

        [Test]
        public void ValidateNpmPath_AnyNonEmptyString_ReturnsTrue()
        {
            // ValidateNpmPath는 string.IsNullOrEmpty만 검사 — 실제 파일 존재 검증은 하지 않음
            Assert.IsTrue(PathValidator.ValidateNpmPath("/usr/local/bin/npm"));
        }

        [Test]
        public void ValidateSettings_NullConfig_ReturnsFalse()
        {
            bool result = PathValidator.ValidateSettings(null);
            Assert.IsFalse(result);
        }

        [Test]
        public void ValidateSettings_NonNullConfig_ReturnsTrue()
        {
            var config = ScriptableObject.CreateInstance<AITEditorScriptObject>();
            try
            {
                bool result = PathValidator.ValidateSettings(config);
                Assert.IsTrue(result);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void ValidateSettingsForPackage_NullConfig_ReturnsFalse()
        {
            bool result = PathValidator.ValidateSettingsForPackage(null);
            Assert.IsFalse(result);
        }

        [Test]
        public void ValidateSettingsForPackage_EmptyAppName_ReturnsFalse()
        {
            var config = ScriptableObject.CreateInstance<AITEditorScriptObject>();
            try
            {
                config.appName = "";
                // appName 비어있으면 AITLog.Error 경로 → LogAssert.Expect 필요
                LogAssert.Expect(LogType.Error, "AIT: App Name이 설정되지 않았습니다.");
                bool result = PathValidator.ValidateSettingsForPackage(config);
                Assert.IsFalse(result);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void ValidateSettingsForPackage_ValidAppName_ReturnsTrue()
        {
            var config = ScriptableObject.CreateInstance<AITEditorScriptObject>();
            try
            {
                config.appName = "test-app";
                bool result = PathValidator.ValidateSettingsForPackage(config);
                Assert.IsTrue(result);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void GetBuildTemplatePath_ReturnsProjectRelativeAitBuildPath()
        {
            string projectPath = UnityUtil.GetProjectPath();
            string expected = Path.Combine(projectPath, "ait-build");
            string actual = PathValidator.GetBuildTemplatePath();
            Assert.AreEqual(expected, actual);
        }
    }
}
