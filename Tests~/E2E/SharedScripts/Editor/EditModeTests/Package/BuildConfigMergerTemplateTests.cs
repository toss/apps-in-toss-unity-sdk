// -----------------------------------------------------------------------
// BuildConfigMergerTemplateTests.cs - UpdateViteConfig 단위 테스트
// Level 0: 임시 파일 fixture로 플레이스홀더 치환 + USER_CONFIG 마커 보존 검증
// -----------------------------------------------------------------------

using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace AppsInToss.Editor.Package.Tests
{
    [TestFixture]
    public class BuildConfigMergerTemplateTests
    {
        private string _projectDir;
        private string _sdkDir;
        private string _destDir;
        private AITEditorScriptObject _config;

        [SetUp]
        public void SetupTempDirsAndConfig()
        {
            string root = Path.Combine(Path.GetTempPath(), "ait-bcm-tpl-tests-" + System.Guid.NewGuid().ToString("N"));
            _projectDir = Path.Combine(root, "project");
            _sdkDir = Path.Combine(root, "sdk");
            _destDir = Path.Combine(root, "dest");
            Directory.CreateDirectory(_projectDir);
            Directory.CreateDirectory(_sdkDir);
            Directory.CreateDirectory(_destDir);

            // ScriptableObject는 CreateInstance로 생성해야 함
            _config = ScriptableObject.CreateInstance<AITEditorScriptObject>();
            _config.viteHost = "test.example.com";
            _config.vitePort = 4242;
            _config.appName = "test-app";
            _config.displayName = "Test App";
            _config.primaryColor = "#FF0000";
            _config.iconUrl = "https://example.com/icon.png";
            _config.outdir = "test-dist";
        }

        [TearDown]
        public void Cleanup()
        {
            if (_projectDir != null)
            {
                string root = Path.GetDirectoryName(_projectDir);
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }

            if (_config != null)
            {
                Object.DestroyImmediate(_config);
            }
        }

        // ----- UpdateViteConfig -----

        [Test]
        public void UpdateViteConfig_SubstitutesViteHostAndPort_WhenPlaceholdersPresent()
        {
            File.WriteAllText(Path.Combine(_sdkDir, "vite.config.ts"),
                "export default { server: { host: '%AIT_VITE_HOST%', port: %AIT_VITE_PORT% } }");

            BuildConfigMerger.UpdateViteConfig(_projectDir, _sdkDir, _destDir, _config);

            string merged = File.ReadAllText(Path.Combine(_destDir, "vite.config.ts"));
            StringAssert.Contains("test.example.com", merged);
            StringAssert.Contains("4242", merged);
            Assert.IsFalse(merged.Contains("%AIT_VITE_HOST%"), "플레이스홀더가 남아있음");
            Assert.IsFalse(merged.Contains("%AIT_VITE_PORT%"), "플레이스홀더가 남아있음");
        }

        [Test]
        public void UpdateViteConfig_PreservesProjectUserConfigSection_WhenMarkerPresent()
        {
            // SDK 템플릿 - USER_CONFIG 마커 영역에 SDK 기본값
            string sdkTemplate =
                "// header\n" +
                "//// USER_CONFIG_START ////\n" +
                "// SDK default user config\n" +
                "//// USER_CONFIG_END ////\n" +
                "// footer\n";
            File.WriteAllText(Path.Combine(_sdkDir, "vite.config.ts"), sdkTemplate);

            // 프로젝트 파일 - 같은 마커 안에 사용자 커스텀 코드
            string projectContent =
                "// old header (무시되어야 함)\n" +
                "//// USER_CONFIG_START ////\n" +
                "// USER CUSTOM PLUGIN CONFIG\n" +
                "//// USER_CONFIG_END ////\n" +
                "// old footer (무시되어야 함)\n";
            File.WriteAllText(Path.Combine(_projectDir, "vite.config.ts"), projectContent);

            BuildConfigMerger.UpdateViteConfig(_projectDir, _sdkDir, _destDir, _config);

            string merged = File.ReadAllText(Path.Combine(_destDir, "vite.config.ts"));
            // 사용자 커스텀이 보존되어야 함
            StringAssert.Contains("USER CUSTOM PLUGIN CONFIG", merged);
            // SDK 헤더/푸터는 유지되어야 함 (마커 외부는 SDK 템플릿 기반)
            StringAssert.Contains("// header", merged);
            StringAssert.Contains("// footer", merged);
            // 프로젝트의 마커 외부 코드는 들어오면 안 됨
            Assert.IsFalse(merged.Contains("old header"), "마커 외부 영역은 SDK 기준이어야 함");
            Assert.IsFalse(merged.Contains("old footer"), "마커 외부 영역은 SDK 기준이어야 함");
            // SDK 기본 USER_CONFIG는 사용자 것으로 교체되어야 함
            Assert.IsFalse(merged.Contains("SDK default user config"), "SDK 기본 USER_CONFIG가 교체되지 않음");
        }

        [Test]
        public void UpdateViteConfig_FallsBackToSdkContent_WhenProjectFileMissing()
        {
            File.WriteAllText(Path.Combine(_sdkDir, "vite.config.ts"),
                "// SDK only - host=%AIT_VITE_HOST%");

            // 프로젝트 파일 없음
            BuildConfigMerger.UpdateViteConfig(_projectDir, _sdkDir, _destDir, _config);

            string merged = File.ReadAllText(Path.Combine(_destDir, "vite.config.ts"));
            StringAssert.Contains("// SDK only", merged);
            StringAssert.Contains("test.example.com", merged);
        }

        // ----- GetNavigationBarJson -----

        [Test]
        public void GetNavigationBarJson_EmitsTransparentBackgroundTrue_WhenGameAndDefault()
        {
            var cfg = ScriptableObject.CreateInstance<AITEditorScriptObject>();
            try
            {
                cfg.webViewType = 0; // game
                // navigationBarTransparentBackground 기본값 true, theme 기본값 0(미지정)
                string json = cfg.GetNavigationBarJson();
                StringAssert.Contains("transparentBackground: true", json);
                Assert.IsFalse(json.Contains("theme:"), "theme 미지정일 때 theme 키가 없어야 함");
            }
            finally { Object.DestroyImmediate(cfg); }
        }

        [Test]
        public void GetNavigationBarJson_EmitsTheme_WhenGameAndThemeSet()
        {
            var cfg = ScriptableObject.CreateInstance<AITEditorScriptObject>();
            try
            {
                cfg.webViewType = 0; // game
                cfg.navigationBarTheme = 2; // dark
                string json = cfg.GetNavigationBarJson();
                StringAssert.Contains("transparentBackground: true", json);
                StringAssert.Contains("theme: 'dark'", json);
            }
            finally { Object.DestroyImmediate(cfg); }
        }

        [Test]
        public void GetNavigationBarJson_EmitsTransparentBackgroundFalse_WhenGameAndToggleOff()
        {
            var cfg = ScriptableObject.CreateInstance<AITEditorScriptObject>();
            try
            {
                cfg.webViewType = 0; // game
                cfg.navigationBarTransparentBackground = false;
                string json = cfg.GetNavigationBarJson();
                StringAssert.Contains("transparentBackground: false", json);
            }
            finally { Object.DestroyImmediate(cfg); }
        }

        [Test]
        public void GetNavigationBarJson_ReturnsEmptyObject_WhenPartner()
        {
            var cfg = ScriptableObject.CreateInstance<AITEditorScriptObject>();
            try
            {
                cfg.webViewType = 1; // partner
                cfg.navigationBarTransparentBackground = true;
                cfg.navigationBarTheme = 2;
                // 하위호환: partner는 navigationBar 필드를 emit하지 않음
                Assert.AreEqual("{}", cfg.GetNavigationBarJson());
            }
            finally { Object.DestroyImmediate(cfg); }
        }

        // ----- UpdateAppsInTossConfig (3.x) -----

        [Test]
        public void UpdateAppsInTossConfig_SubstitutesNavigationBarPlaceholder_NoLeak()
        {
            File.WriteAllText(Path.Combine(_sdkDir, "apps-in-toss.config.ts"),
                "export default {\n" +
                "  name: '%AIT_APP_NAME%',\n" +
                "  navigationBar: %AIT_NAVIGATION_BAR%,\n" +
                "}");

            BuildConfigMerger.UpdateAppsInTossConfig(_projectDir, _sdkDir, _destDir, _config);

            string merged = File.ReadAllText(Path.Combine(_destDir, "apps-in-toss.config.ts"));
            // webViewType 기본값(0=game) → transparentBackground emit
            StringAssert.Contains("transparentBackground: true", merged);
            Assert.IsFalse(Regex.IsMatch(merged, "%AIT_[A-Z_]+%"),
                $"apps-in-toss.config.ts에 치환되지 않은 %AIT_*% 플레이스홀더가 남아있음:\n{merged}");
        }
    }
}
