// -----------------------------------------------------------------------
// AITBuildValidatorConfigGuardTests.cs - ValidateGeneratedBuildConfigs(①) 단위 테스트
// Level 0: 생성된 apps-in-toss.config.ts/granite.config.ts의 미치환 플레이스홀더 가드 검증
//   - SDK_GENERATED 미치환 → 치명적(PLACEHOLDER_SUBSTITUTION_FAILED)
//   - USER_CONFIG 미치환/2.x deprecated 키 → 경고만(SUCCEED)
// -----------------------------------------------------------------------

using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using AppsInToss;          // AITConvertCore (namespace AppsInToss)
using AppsInToss.Editor;   // AITBuildValidator (namespace AppsInToss.Editor)

[TestFixture]
public class AITBuildValidatorConfigGuardTests
{
    private string _buildDir;

    private const string Substituted =
        "import { defineConfig } from '@apps-in-toss/web-framework/config';\n" +
        "//// SDK_GENERATED_START - DO NOT EDIT THIS SECTION ////\n" +
        "const sdkConfig = {\n" +
        "  appName: 'guard-app',\n" +
        "  brand: { primaryColor: '#3182f6' },\n" +
        "  permissions: [],\n" +
        "  webView: { allowsInlineMediaPlayback: true, mediaPlaybackRequiresUserAction: false },\n" +
        "  webBundleDir: 'dist/web',\n" +
        "};\n" +
        "//// SDK_GENERATED_END ////\n" +
        // USER_CONFIG 블록이 삽입되는 자리(sentinel). 반드시 string.Replace로 치환할 것 —
        // string.Format을 쓰면 위 TS 리터럴 중괄호({ ... })를 서식 placeholder로 오인해 FormatException이 발생한다.
        "{0}" +
        "const _userConfig = userConfig as Record<string, any>;\n" +
        "export default defineConfig({ ..._userConfig, ...sdkConfig });\n";

    private const string EmptyUserConfig =
        "//// USER_CONFIG_START ////\n" +
        "const userConfig = {\n" +
        "  // 여기에 사용자 커스텀 설정을 추가하세요\n" +
        "};\n" +
        "//// USER_CONFIG_END ////\n";

    [SetUp]
    public void Setup()
    {
        _buildDir = Path.Combine(Path.GetTempPath(), "ait-guard-tests-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_buildDir);
    }

    [TearDown]
    public void Cleanup()
    {
        if (_buildDir != null && Directory.Exists(_buildDir))
            Directory.Delete(_buildDir, recursive: true);
    }

    private void WriteAppsInToss(string content)
    {
        File.WriteAllText(Path.Combine(_buildDir, "apps-in-toss.config.ts"), content);
    }

    [Test]
    public void Validate_CleanConfig_ReturnsSucceed()
    {
        WriteAppsInToss(Substituted.Replace("{0}", EmptyUserConfig));

        var result = AITBuildValidator.ValidateGeneratedBuildConfigs(_buildDir);

        Assert.AreEqual(AITConvertCore.AITExportError.SUCCEED, result);
    }

    [Test]
    public void Validate_NoConfigFiles_ReturnsSucceed()
    {
        var result = AITBuildValidator.ValidateGeneratedBuildConfigs(_buildDir);
        Assert.AreEqual(AITConvertCore.AITExportError.SUCCEED, result);
    }

    [Test]
    public void Validate_PlaceholderInSdkGenerated_ReturnsPlaceholderFailure()
    {
        // SDK_GENERATED 영역에 미치환 %AIT_PRIMARY_COLOR% 가 남은 상태
        string bad =
            "//// SDK_GENERATED_START ////\n" +
            "const sdkConfig = { appName: 'x', brand: { primaryColor: '%AIT_PRIMARY_COLOR%' } };\n" +
            "//// SDK_GENERATED_END ////\n" +
            EmptyUserConfig +
            "export default sdkConfig;\n";
        WriteAppsInToss(bad);

        LogAssert.Expect(LogType.Error, new Regex(@"미치환 플레이스홀더"));
        var result = AITBuildValidator.ValidateGeneratedBuildConfigs(_buildDir);

        Assert.AreEqual(AITConvertCore.AITExportError.PLACEHOLDER_SUBSTITUTION_FAILED, result);
    }

    [Test]
    public void Validate_PlaceholderOnlyInUserConfig_ReturnsSucceed_WithWarning()
    {
        // 잘못 포팅된 USER_CONFIG: brand 블록을 통째로 복사 → 미치환 플레이스홀더 포함
        // 병합에서 SDK 값이 우선하므로 치명적이지 않음(경고만, 빌드 계속)
        string userConfig =
            "//// USER_CONFIG_START ////\n" +
            "const userConfig = {\n" +
            "  brand: { primaryColor: '%AIT_PRIMARY_COLOR%', displayName: '%AIT_DISPLAY_NAME%' },\n" +
            "};\n" +
            "//// USER_CONFIG_END ////\n";
        WriteAppsInToss(Substituted.Replace("{0}", userConfig));

        LogAssert.Expect(LogType.Warning, new Regex(@"USER_CONFIG에 SDK가 관리하는 설정이 남아"));
        var result = AITBuildValidator.ValidateGeneratedBuildConfigs(_buildDir);

        Assert.AreEqual(AITConvertCore.AITExportError.SUCCEED, result);
    }

    [Test]
    public void Validate_DeprecatedKeyInUserConfig_ReturnsSucceed_WithWarning()
    {
        // 2.x 키(webViewProps)가 USER_CONFIG에 남은 경우 → 경고만
        string userConfig =
            "//// USER_CONFIG_START ////\n" +
            "const userConfig = {\n" +
            "  webViewProps: { bounces: false },\n" +
            "};\n" +
            "//// USER_CONFIG_END ////\n";
        WriteAppsInToss(Substituted.Replace("{0}", userConfig));

        LogAssert.Expect(LogType.Warning, new Regex(@"이동/이름변경된 키"));
        var result = AITBuildValidator.ValidateGeneratedBuildConfigs(_buildDir);

        Assert.AreEqual(AITConvertCore.AITExportError.SUCCEED, result);
    }
}
