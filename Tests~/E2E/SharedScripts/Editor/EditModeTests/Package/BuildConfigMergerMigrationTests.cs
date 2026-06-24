// -----------------------------------------------------------------------
// BuildConfigMergerMigrationTests.cs - UpdateAppsInTossConfig USER_CONFIG 자동 이전(②) 단위 테스트
// Level 0: granite.config.ts → apps-in-toss.config.ts 마이그레이션 + 빈/내용 판별 검증
// -----------------------------------------------------------------------

using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace AppsInToss.Editor.Package.Tests
{
    [TestFixture]
    public class BuildConfigMergerMigrationTests
    {
        private string _projectDir;
        private string _sdkDir;
        private string _destDir;
        private AITEditorScriptObject _config;

        private const string SdkAppsInTossTemplate =
            "import { defineConfig } from '@apps-in-toss/web-framework/config';\n" +
            "//// SDK_GENERATED_START - DO NOT EDIT THIS SECTION ////\n" +
            "const sdkConfig = {\n" +
            "  appName: '%AIT_APP_NAME%',\n" +
            "  brand: { primaryColor: '%AIT_PRIMARY_COLOR%' },\n" +
            "  permissions: %AIT_PERMISSIONS%,\n" +
            "  webView: {\n" +
            "    allowsInlineMediaPlayback: %AIT_ALLOWS_INLINE_MEDIA_PLAYBACK%,\n" +
            "    mediaPlaybackRequiresUserAction: %AIT_MEDIA_PLAYBACK_REQUIRES_USER_ACTION%,\n" +
            "  },\n" +
            "  webBundleDir: 'dist/web',\n" +
            "};\n" +
            "//// SDK_GENERATED_END ////\n" +
            "//// USER_CONFIG_START ////\n" +
            "const userConfig = {\n" +
            "  // 여기에 사용자 커스텀 설정을 추가하세요\n" +
            "};\n" +
            "//// USER_CONFIG_END ////\n" +
            "const _userConfig = userConfig as Record<string, any>;\n" +
            "export default defineConfig({ ..._userConfig, ...sdkConfig });\n";

        [SetUp]
        public void Setup()
        {
            string root = Path.Combine(Path.GetTempPath(), "ait-bcm-mig-tests-" + System.Guid.NewGuid().ToString("N"));
            _projectDir = Path.Combine(root, "project");
            _sdkDir = Path.Combine(root, "sdk");
            _destDir = Path.Combine(root, "dest");
            Directory.CreateDirectory(_projectDir);
            Directory.CreateDirectory(_sdkDir);
            Directory.CreateDirectory(_destDir);

            _config = ScriptableObject.CreateInstance<AITEditorScriptObject>();
            _config.appName = "mig-app";
            _config.primaryColor = "#123456";
        }

        [TearDown]
        public void Cleanup()
        {
            if (_projectDir != null)
            {
                string root = Path.GetDirectoryName(_projectDir);
                if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            }
            if (_config != null) Object.DestroyImmediate(_config);
        }

        private static string GraniteWithUserConfig(string userConfigBody)
        {
            return
                "import { defineConfig } from '@granite-js/react-native/config';\n" +
                "//// SDK_GENERATED_START ////\n" +
                "const sdkConfig = { appName: '%AIT_APP_NAME%' };\n" +
                "//// SDK_GENERATED_END ////\n" +
                "//// USER_CONFIG_START ////\n" +
                "const userConfig = {\n" +
                userConfigBody +
                "};\n" +
                "//// USER_CONFIG_END ////\n" +
                "export default defineConfig({ ...sdkConfig, ...userConfig });\n";
        }

        // ----- IsEffectivelyEmptyUserConfig -----

        [Test]
        public void IsEffectivelyEmptyUserConfig_DefaultCommentOnly_ReturnsTrue()
        {
            string section =
                "//// USER_CONFIG_START ////\n" +
                "const userConfig = {\n" +
                "  // 여기에 사용자 커스텀 설정을 추가하세요\n" +
                "};\n" +
                "//// USER_CONFIG_END ////";
            Assert.IsTrue(BuildConfigMerger.IsEffectivelyEmptyUserConfig(section));
        }

        [Test]
        public void IsEffectivelyEmptyUserConfig_WithRealProperty_ReturnsFalse()
        {
            string section =
                "//// USER_CONFIG_START ////\n" +
                "const userConfig = {\n" +
                "  myCustomPlugin: true,\n" +
                "};\n" +
                "//// USER_CONFIG_END ////";
            Assert.IsFalse(BuildConfigMerger.IsEffectivelyEmptyUserConfig(section));
        }

        [Test]
        public void IsEffectivelyEmptyUserConfig_BlockCommentOnly_ReturnsTrue()
        {
            string section =
                "//// USER_CONFIG_START ////\n" +
                "const userConfig = {\n" +
                "  /* nothing here yet */\n" +
                "};\n" +
                "//// USER_CONFIG_END ////";
            Assert.IsTrue(BuildConfigMerger.IsEffectivelyEmptyUserConfig(section));
        }

        // ----- ResolveUserConfigForAppsInToss -----

        [Test]
        public void Resolve_PrefersAppsInTossUserConfig_WhenNonEmpty()
        {
            File.WriteAllText(Path.Combine(_projectDir, "apps-in-toss.config.ts"),
                SdkAppsInTossTemplate.Replace(
                    "  // 여기에 사용자 커스텀 설정을 추가하세요\n",
                    "  fromAppsInToss: 1,\n"));
            File.WriteAllText(Path.Combine(_projectDir, "granite.config.ts"),
                GraniteWithUserConfig("  fromGranite: 2,\n"));

            string section = BuildConfigMerger.ResolveUserConfigForAppsInToss(_projectDir);

            StringAssert.Contains("fromAppsInToss", section);
            Assert.IsFalse(section.Contains("fromGranite"), "apps-in-toss USER_CONFIG가 우선되어야 함");
        }

        [Test]
        public void Resolve_MigratesGraniteUserConfig_WhenAppsInTossEmpty()
        {
            // apps-in-toss.config.ts는 비어 있고(기본 주석만), granite에는 커스텀 설정 존재
            File.WriteAllText(Path.Combine(_projectDir, "apps-in-toss.config.ts"), SdkAppsInTossTemplate);
            File.WriteAllText(Path.Combine(_projectDir, "granite.config.ts"),
                GraniteWithUserConfig("  fromGranite: 2,\n"));

            string section = BuildConfigMerger.ResolveUserConfigForAppsInToss(_projectDir);

            Assert.IsNotNull(section);
            StringAssert.Contains("fromGranite", section);
        }

        [Test]
        public void Resolve_MigratesGraniteUserConfig_WhenNoAppsInTossFile()
        {
            // apps-in-toss.config.ts 자체가 없는 2.x 프로젝트
            File.WriteAllText(Path.Combine(_projectDir, "granite.config.ts"),
                GraniteWithUserConfig("  fromGranite: 3,\n"));

            string section = BuildConfigMerger.ResolveUserConfigForAppsInToss(_projectDir);

            Assert.IsNotNull(section);
            StringAssert.Contains("fromGranite", section);
        }

        [Test]
        public void Resolve_ReturnsNull_WhenBothEmpty()
        {
            File.WriteAllText(Path.Combine(_projectDir, "apps-in-toss.config.ts"), SdkAppsInTossTemplate);
            File.WriteAllText(Path.Combine(_projectDir, "granite.config.ts"),
                GraniteWithUserConfig("  // 여기에 사용자 커스텀 설정을 추가하세요\n"));

            string section = BuildConfigMerger.ResolveUserConfigForAppsInToss(_projectDir);

            Assert.IsNull(section, "둘 다 비어 있으면 SDK 템플릿 기본값(null) 유지");
        }

        [Test]
        public void Resolve_ReturnsNull_WhenNoFiles()
        {
            Assert.IsNull(BuildConfigMerger.ResolveUserConfigForAppsInToss(_projectDir));
        }

        // ----- UpdateAppsInTossConfig end-to-end (마이그레이션 경로) -----

        [Test]
        public void UpdateAppsInTossConfig_MigratesGraniteUserConfig_AndSubstitutesSdkPlaceholders()
        {
            // SDK 템플릿
            File.WriteAllText(Path.Combine(_sdkDir, "apps-in-toss.config.ts"), SdkAppsInTossTemplate);
            // 프로젝트: apps-in-toss 없음, granite에 커스텀 USER_CONFIG
            File.WriteAllText(Path.Combine(_projectDir, "granite.config.ts"),
                GraniteWithUserConfig("  myCustomPlugin: true,\n"));

            BuildConfigMerger.UpdateAppsInTossConfig(_projectDir, _sdkDir, _destDir, _config);

            string merged = File.ReadAllText(Path.Combine(_destDir, "apps-in-toss.config.ts"));
            // granite USER_CONFIG가 이전됨
            StringAssert.Contains("myCustomPlugin", merged);
            // SDK_GENERATED 플레이스홀더는 치환됨
            StringAssert.Contains("mig-app", merged);
            StringAssert.Contains("#123456", merged);
            Assert.IsFalse(Regex.IsMatch(merged, "%AIT_(APP_NAME|PRIMARY_COLOR)%"),
                "SDK_GENERATED 플레이스홀더가 남아있음:\n" + merged);
        }

        [Test]
        public void UpdateAppsInTossConfig_NoOp_WhenSdkTemplateMissing()
        {
            // SDK 템플릿이 없으면(구버전 SDK) dest 파일을 만들지 않는다
            BuildConfigMerger.UpdateAppsInTossConfig(_projectDir, _sdkDir, _destDir, _config);
            Assert.IsFalse(File.Exists(Path.Combine(_destDir, "apps-in-toss.config.ts")));
        }
    }
}
