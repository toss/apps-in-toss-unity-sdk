// -----------------------------------------------------------------------
// BuildConfigMergerTemplateTests.cs - UpdateViteConfig / UpdateGraniteConfig 단위 테스트
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

        // ----- UpdateGraniteConfig -----

        [Test]
        public void UpdateGraniteConfig_SubstitutesAllPlaceholders_WhenAllPresent()
        {
            // UpdateGraniteConfig가 치환하는 12개 플레이스홀더 전부 포함
            File.WriteAllText(Path.Combine(_sdkDir, "granite.config.ts"),
                "export default {\n" +
                "  name: '%AIT_APP_NAME%',\n" +
                "  display: '%AIT_DISPLAY_NAME%',\n" +
                "  color: '%AIT_PRIMARY_COLOR%',\n" +
                "  icon: '%AIT_ICON_URL%',\n" +
                "  bridgeColorMode: '%AIT_BRIDGE_COLOR_MODE%',\n" +
                "  webViewType: '%AIT_WEBVIEW_TYPE%',\n" +
                "  allowsInlineMediaPlayback: %AIT_ALLOWS_INLINE_MEDIA_PLAYBACK%,\n" +
                "  mediaPlaybackRequiresUserAction: %AIT_MEDIA_PLAYBACK_REQUIRES_USER_ACTION%,\n" +
                "  viteHost: '%AIT_VITE_HOST%',\n" +
                "  vitePort: %AIT_VITE_PORT%,\n" +
                "  permissions: %AIT_PERMISSIONS%,\n" +
                "  outdir: '%AIT_OUTDIR%'\n" +
                "}");

            BuildConfigMerger.UpdateGraniteConfig(_projectDir, _sdkDir, _destDir, _config);

            string merged = File.ReadAllText(Path.Combine(_destDir, "granite.config.ts"));
            StringAssert.Contains("test-app", merged);
            StringAssert.Contains("Test App", merged);
            StringAssert.Contains("#FF0000", merged);
            StringAssert.Contains("https://example.com/icon.png", merged);
            StringAssert.Contains("test.example.com", merged);
            StringAssert.Contains("4242", merged);
            StringAssert.Contains("test-dist", merged);
            // GetBridgeColorModeString/GetWebViewTypeString/GetPermissionsJson 호출 확인 — 결과 토큰이 사라지면 됨
            Assert.IsFalse(Regex.IsMatch(merged, "%AIT_[A-Z_]+%"),
                $"치환되지 않은 %AIT_*% 플레이스홀더가 남아있음:\n{merged}");
        }

        [Test]
        public void UpdateGraniteConfig_PreservesProjectUserConfigSection_WhenMarkerPresent()
        {
            string sdkTemplate =
                "// granite header\n" +
                "//// USER_CONFIG_START ////\n" +
                "// SDK granite user config\n" +
                "//// USER_CONFIG_END ////\n" +
                "// granite footer\n";
            File.WriteAllText(Path.Combine(_sdkDir, "granite.config.ts"), sdkTemplate);

            string projectContent =
                "//// USER_CONFIG_START ////\n" +
                "// USER GRANITE OVERRIDES\n" +
                "//// USER_CONFIG_END ////\n";
            File.WriteAllText(Path.Combine(_projectDir, "granite.config.ts"), projectContent);

            BuildConfigMerger.UpdateGraniteConfig(_projectDir, _sdkDir, _destDir, _config);

            string merged = File.ReadAllText(Path.Combine(_destDir, "granite.config.ts"));
            StringAssert.Contains("USER GRANITE OVERRIDES", merged);
            StringAssert.Contains("// granite header", merged);
            StringAssert.Contains("// granite footer", merged);
            Assert.IsFalse(merged.Contains("SDK granite user config"), "SDK 기본 USER_CONFIG가 교체되지 않음");
        }

        [Test]
        public void UpdateGraniteConfig_FallsBackToSdkContent_WhenProjectFileMissing()
        {
            File.WriteAllText(Path.Combine(_sdkDir, "granite.config.ts"),
                "// SDK granite only - app=%AIT_APP_NAME% color=%AIT_PRIMARY_COLOR% port=%AIT_VITE_PORT%");

            BuildConfigMerger.UpdateGraniteConfig(_projectDir, _sdkDir, _destDir, _config);

            string merged = File.ReadAllText(Path.Combine(_destDir, "granite.config.ts"));
            StringAssert.Contains("// SDK granite only", merged);
            StringAssert.Contains("test-app", merged);
            StringAssert.Contains("#FF0000", merged);
            StringAssert.Contains("4242", merged);
            Assert.IsFalse(Regex.IsMatch(merged, "%AIT_[A-Z_]+%"),
                "프로젝트 파일이 없을 때도 SDK 템플릿의 플레이스홀더는 모두 치환되어야 함");
        }
    }
}
