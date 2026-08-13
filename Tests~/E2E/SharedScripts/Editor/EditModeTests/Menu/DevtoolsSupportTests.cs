// -----------------------------------------------------------------------
// DevtoolsSupportTests.cs
// Level 0: DevtoolsSupport — devtools mock 활성화 게이트(ShouldEnable) 및
// vite.config.ts가 읽는 환경변수 구성(AddEnvVars) 검증
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using AppsInToss;
using AppsInToss.Editor;
using AppsInToss.Editor.Menu;

[TestFixture]
public class DevtoolsSupportTests
{
    private string tempDir;
    private AITEditorScriptObject config;
    private string originalEnvOverride;

    [SetUp]
    public void SetUp()
    {
        tempDir = Path.Combine(Path.GetTempPath(), "ait-test-devtools-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        config = ScriptableObject.CreateInstance<AITEditorScriptObject>();
        config.devtools.enabled = true;
        config.devtools.panel = true;
        config.devtools.mcp = false;

        // 실행 환경에 AIT_DEVTOOLS가 이미 설정돼 있으면(예: 다른 프로세스) 테스트가
        // 오염되므로 저장 후 제거하고, TearDown에서 원복한다.
        originalEnvOverride = Environment.GetEnvironmentVariable("AIT_DEVTOOLS");
        Environment.SetEnvironmentVariable("AIT_DEVTOOLS", null);
    }

    [TearDown]
    public void TearDown()
    {
        Environment.SetEnvironmentVariable("AIT_DEVTOOLS", originalEnvOverride);

        if (config != null)
        {
            UnityEngine.Object.DestroyImmediate(config);
        }

        if (Directory.Exists(tempDir))
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private void WriteDevtoolsInstalled()
    {
        string pkgDir = Path.Combine(tempDir, "node_modules", "@apps-in-toss", "devtools");
        Directory.CreateDirectory(pkgDir);
        File.WriteAllText(Path.Combine(pkgDir, "package.json"), "{\"name\":\"@apps-in-toss/devtools\"}");
    }

    // ---------------------------------------------------------------
    // IsDevtoolsInstalled
    // ---------------------------------------------------------------

    [Test]
    public void IsDevtoolsInstalled_PackageJsonMissing_ReturnsFalse()
    {
        Assert.IsFalse(DevtoolsSupport.IsDevtoolsInstalled(tempDir));
    }

    [Test]
    public void IsDevtoolsInstalled_PackageJsonPresent_ReturnsTrue()
    {
        WriteDevtoolsInstalled();
        Assert.IsTrue(DevtoolsSupport.IsDevtoolsInstalled(tempDir));
    }

    // ---------------------------------------------------------------
    // ShouldEnable — 게이트 순서: env 오버라이드 →
    // config.devtools.enabled → viteOnly → devtools 설치 확인
    // ---------------------------------------------------------------

    [Test]
    public void ShouldEnable_DevtoolsNotInstalled_ReturnsFalseWithReason()
    {
        // node_modules/@apps-in-toss/devtools 없음, 그 외 조건은 모두 충족.
        bool result = DevtoolsSupport.ShouldEnable(config, tempDir, viteOnly: true, out string reason);

        Assert.IsFalse(result);
        Assert.IsFalse(string.IsNullOrEmpty(reason), "reason은 항상 비어있지 않아야 함");
    }

    [Test]
    public void ShouldEnable_ViteOnlyFalse_ReturnsFalse()
    {
        WriteDevtoolsInstalled();

        bool result = DevtoolsSupport.ShouldEnable(config, tempDir, viteOnly: false, out string reason);

        Assert.IsFalse(result);
        Assert.IsFalse(string.IsNullOrEmpty(reason));
    }

    [Test]
    public void ShouldEnable_ConfigDevtoolsDisabled_ReturnsFalse()
    {
        WriteDevtoolsInstalled();
        config.devtools.enabled = false;

        bool result = DevtoolsSupport.ShouldEnable(config, tempDir, viteOnly: true, out string reason);

        Assert.IsFalse(result);
        Assert.IsFalse(string.IsNullOrEmpty(reason));
    }

    [Test]
    public void ShouldEnable_AllConditionsMet_ReturnsTrue()
    {
        WriteDevtoolsInstalled();

        bool result = DevtoolsSupport.ShouldEnable(config, tempDir, viteOnly: true, out string reason);

        Assert.IsTrue(result);
        Assert.IsFalse(string.IsNullOrEmpty(reason));
    }

    [Test]
    public void ShouldEnable_EnvOverrideZero_ReturnsFalse()
    {
        // 그 외 조건은 모두 충족해도 환경변수 오버라이드가 최우선으로 비활성화해야 함.
        WriteDevtoolsInstalled();
        Environment.SetEnvironmentVariable("AIT_DEVTOOLS", "0");
        try
        {
            bool result = DevtoolsSupport.ShouldEnable(config, tempDir, viteOnly: true, out string reason);

            Assert.IsFalse(result);
            StringAssert.Contains("AIT_DEVTOOLS", reason);
        }
        finally
        {
            Environment.SetEnvironmentVariable("AIT_DEVTOOLS", null);
        }
    }

    [Test]
    public void ShouldEnable_EnvOverrideFalse_CaseInsensitive_ReturnsFalse()
    {
        WriteDevtoolsInstalled();
        Environment.SetEnvironmentVariable("AIT_DEVTOOLS", "FALSE");
        try
        {
            bool result = DevtoolsSupport.ShouldEnable(config, tempDir, viteOnly: true, out string reason);

            Assert.IsFalse(result);
        }
        finally
        {
            Environment.SetEnvironmentVariable("AIT_DEVTOOLS", null);
        }
    }

    [Test]
    public void ShouldEnable_NullConfig_ReturnsFalse()
    {
        bool result = DevtoolsSupport.ShouldEnable(null, tempDir, viteOnly: true, out string reason);

        Assert.IsFalse(result);
        Assert.IsFalse(string.IsNullOrEmpty(reason));
    }

    // ---------------------------------------------------------------
    // AddEnvVars
    // ---------------------------------------------------------------

    [Test]
    public void AddEnvVars_Enabled_SetsDevtoolsAndPanelOn()
    {
        config.devtools.panel = true;
        config.devtools.mcp = false;
        var envVars = new Dictionary<string, string>();

        DevtoolsSupport.AddEnvVars(envVars, config, enabled: true);

        Assert.AreEqual("1", envVars["AIT_DEVTOOLS"]);
        Assert.AreEqual("1", envVars["AIT_DEVTOOLS_PANEL"]);
        Assert.IsFalse(envVars.ContainsKey("AIT_DEVTOOLS_MCP"), "mcp=false면 AIT_DEVTOOLS_MCP 키 자체가 없어야 함");
    }

    [Test]
    public void AddEnvVars_Enabled_PanelOff_SetsPanelZero()
    {
        config.devtools.panel = false;
        var envVars = new Dictionary<string, string>();

        DevtoolsSupport.AddEnvVars(envVars, config, enabled: true);

        Assert.AreEqual("1", envVars["AIT_DEVTOOLS"]);
        Assert.AreEqual("0", envVars["AIT_DEVTOOLS_PANEL"]);
    }

    [Test]
    public void AddEnvVars_Enabled_McpOn_SetsMcpOne()
    {
        config.devtools.mcp = true;
        var envVars = new Dictionary<string, string>();

        DevtoolsSupport.AddEnvVars(envVars, config, enabled: true);

        Assert.AreEqual("1", envVars["AIT_DEVTOOLS_MCP"]);
    }

    [Test]
    public void AddEnvVars_Disabled_SetsDevtoolsZero()
    {
        var envVars = new Dictionary<string, string>();

        DevtoolsSupport.AddEnvVars(envVars, config, enabled: false);

        Assert.AreEqual("0", envVars["AIT_DEVTOOLS"]);
    }

    [Test]
    public void AddEnvVars_NullEnvVars_DoesNotThrow()
    {
        Assert.DoesNotThrow(() => DevtoolsSupport.AddEnvVars(null, config, enabled: true));
    }

    // ---------------------------------------------------------------
    // AITDevtoolsSettings 역직렬화 — zero-fill 기본값 가드
    //
    // 구버전 AITConfig.asset(YAML)에는 devtools 블록이 없다. Unity는 중첩
    // [Serializable] 클래스를 역직렬화할 때 생성자/필드 초기화식을 실행하지
    // 않고 zero-fill하므로, AITDevtoolsSettings의 직렬화 필드가 긍정형
    // (enabled/panel)이면 zero-fill로 false가 되어 devtools가 조용히 꺼진다.
    // 이를 막기 위해 직렬화 필드는 부정형(disableMock/hidePanel)으로 두고
    // enabled/panel은 이를 반전한 프로퍼티로 노출한다.
    //
    // 아래 두 테스트는 서로 다른 각도에서 이 계약을 검증한다:
    // 1) LegacyAsset_WithoutDevtoolsBlock_...: 실제 devtools: 키가 없는 YAML을
    //    AssetDatabase로 임포트/로드해 최종 사용자 시나리오(구버전 asset 오픈)의
    //    관측 가능한 결과가 true/true임을 검증한다. (단, 이 경로는 Unity가
    //    ScriptableObject.CreateInstance 계열로 필드 초기화식을 실행한 뒤 YAML을
    //    덮어쓰므로, 확인 결과 생성자가 항상 실행되어 정상 회귀에서는 버그를
    //    재현하지 못한다 — 그래도 최종 계약은 유효하므로 유지한다.)
    // 2) DisableMockAndHidePanel_ZeroFilledWithoutConstructor_...: Unity 문서가
    //    명시하는 "중첩 [Serializable] 클래스는 역직렬화 시 생성자가 호출되지
    //    않는다"는 실제 매커니즘(도메인 리로드의 backup/restore 등에서 발생)을
    //    FormatterServices.GetUninitializedObject로 직접 재현해, 생성자를 건너뛴
    //    zero-fill 상태에서도 enabled/panel이 true가 됨을 검증한다. 이 테스트가
    //    실질적인 회귀 가드다 — 필드를 disableMock/hidePanel(부정형) 대신
    //    enabled/panel(긍정형)로 되돌리면 이 테스트가 즉시 실패한다.
    // ---------------------------------------------------------------

    // AITEditorScriptObject.cs.meta의 guid — 레거시 asset의 m_Script가 가리켜야 함
    private const string AitEditorScriptObjectGuid = "7c3cbd8251a0c434481614bbcd8082ff";

    [Test]
    public void LegacyAsset_WithoutDevtoolsBlock_DeserializesEnabledAndPanelTrue()
    {
        string assetPath = $"Assets/AIT_LegacyDevtoolsTest_{Guid.NewGuid():N}.asset";

        // devtools: 블록을 의도적으로 생략한 구버전 AITConfig.asset 시뮬레이션.
        // (Tests~/E2E/SampleUnityProject-2022.3/Assets/AppsInToss/Editor/AITConfig.asset 참고, m_Script guid 동일)
        string legacyYaml =
            "%YAML 1.1\n" +
            "%TAG !u! tag:unity3d.com,2011:\n" +
            "--- !u!114 &11400000\n" +
            "MonoBehaviour:\n" +
            "  m_ObjectHideFlags: 0\n" +
            "  m_CorrespondingSourceObject: {fileID: 0}\n" +
            "  m_PrefabInstance: {fileID: 0}\n" +
            "  m_PrefabAsset: {fileID: 0}\n" +
            "  m_GameObject: {fileID: 0}\n" +
            "  m_Enabled: 1\n" +
            "  m_EditorHideFlags: 0\n" +
            $"  m_Script: {{fileID: 11500000, guid: {AitEditorScriptObjectGuid}, type: 3}}\n" +
            "  m_Name: AIT_LegacyDevtoolsTest\n" +
            "  m_EditorClassIdentifier: AppsInTossSDKEditor::AppsInToss.AITEditorScriptObject\n" +
            "  appName: legacy-test\n" +
            "  displayName: Legacy Test\n" +
            "  version: 1.0.0\n" +
            "  description: devtools 블록 없는 구버전 asset 재현용 테스트 픽스처\n" +
            "  primaryColor: '#3182F6'\n" +
            "  iconUrl: https://example.com/icon.png\n" +
            "  bridgeColorMode: 0\n" +
            "  webViewType: 0\n" +
            "  outdir: dist\n";

        try
        {
            File.WriteAllText(assetPath, legacyYaml);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);

            var loaded = AssetDatabase.LoadAssetAtPath<AITEditorScriptObject>(assetPath);

            Assert.IsNotNull(loaded, "레거시 asset 로드에 실패함");
            Assert.IsNotNull(loaded.devtools, "devtools 블록이 없어도 필드는 zero-fill로 non-null 인스턴스여야 함");
            Assert.IsTrue(loaded.devtools.enabled, "devtools 블록 없는 구버전 asset은 enabled 기본값 true여야 함(zero-fill 안전)");
            Assert.IsTrue(loaded.devtools.panel, "devtools 블록 없는 구버전 asset은 panel 기본값 true여야 함(zero-fill 안전)");
            Assert.IsFalse(loaded.devtools.mcp, "mcp는 zero-fill과 기본값이 이미 false로 일치해야 함");
        }
        finally
        {
            AssetDatabase.DeleteAsset(assetPath);
        }
    }

    [Test]
    public void DisableMockAndHidePanel_ZeroFilledWithoutConstructor_EnabledAndPanelStillTrue()
    {
        // Unity 공식 문서(Script Serialization)가 명시하는 대로, 중첩 [Serializable]
        // 클래스는 역직렬화 시 생성자를 거치지 않는 경로가 존재한다(예: 도메인 리로드의
        // backup/restore). FormatterServices.GetUninitializedObject는 바로 그 "생성자를
        // 건너뛴 채 CLR 기본값(bool=false)으로 채워진" 상태를 결정적으로 재현한다.
        var zeroFilled = (AITDevtoolsSettings)FormatterServices.GetUninitializedObject(typeof(AITDevtoolsSettings));

        Assert.IsTrue(zeroFilled.enabled, "직렬화 필드가 zero-fill(false)이어도 enabled는 true여야 한다 — 부정형 필드(disableMock) 반전 계약");
        Assert.IsTrue(zeroFilled.panel, "직렬화 필드가 zero-fill(false)이어도 panel은 true여야 한다 — 부정형 필드(hidePanel) 반전 계약");
        Assert.IsFalse(zeroFilled.mcp, "mcp는 zero-fill(false)과 기본값이 이미 일치해야 한다");
    }
}
