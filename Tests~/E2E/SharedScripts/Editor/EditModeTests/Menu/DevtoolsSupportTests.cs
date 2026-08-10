// -----------------------------------------------------------------------
// DevtoolsSupportTests.cs
// Level 0: DevtoolsSupport — devtools mock 활성화 게이트(ShouldEnable) 및
// vite.config.ts가 읽는 환경변수 구성(AddEnvVars) 검증
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
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
    // ShouldEnable — 게이트 순서: env 오버라이드 → Dev 서버만 →
    // config.devtools.enabled → viteOnly → devtools 설치 확인
    // ---------------------------------------------------------------

    [Test]
    public void ShouldEnable_DevtoolsNotInstalled_ReturnsFalseWithReason()
    {
        // node_modules/@apps-in-toss/devtools 없음, 그 외 조건은 모두 충족.
        bool result = DevtoolsSupport.ShouldEnable(config, ServerType.Dev, tempDir, viteOnly: true, out string reason);

        Assert.IsFalse(result);
        Assert.IsFalse(string.IsNullOrEmpty(reason), "reason은 항상 비어있지 않아야 함");
    }

    [Test]
    public void ShouldEnable_ViteOnlyFalse_ReturnsFalse()
    {
        WriteDevtoolsInstalled();

        bool result = DevtoolsSupport.ShouldEnable(config, ServerType.Dev, tempDir, viteOnly: false, out string reason);

        Assert.IsFalse(result);
        Assert.IsFalse(string.IsNullOrEmpty(reason));
    }

    [Test]
    public void ShouldEnable_ConfigDevtoolsDisabled_ReturnsFalse()
    {
        WriteDevtoolsInstalled();
        config.devtools.enabled = false;

        bool result = DevtoolsSupport.ShouldEnable(config, ServerType.Dev, tempDir, viteOnly: true, out string reason);

        Assert.IsFalse(result);
        Assert.IsFalse(string.IsNullOrEmpty(reason));
    }

    [Test]
    public void ShouldEnable_ServerTypeProd_ReturnsFalse()
    {
        WriteDevtoolsInstalled();

        bool result = DevtoolsSupport.ShouldEnable(config, ServerType.Prod, tempDir, viteOnly: true, out string reason);

        Assert.IsFalse(result);
        Assert.IsFalse(string.IsNullOrEmpty(reason));
    }

    [Test]
    public void ShouldEnable_AllConditionsMet_ReturnsTrue()
    {
        WriteDevtoolsInstalled();

        bool result = DevtoolsSupport.ShouldEnable(config, ServerType.Dev, tempDir, viteOnly: true, out string reason);

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
            bool result = DevtoolsSupport.ShouldEnable(config, ServerType.Dev, tempDir, viteOnly: true, out string reason);

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
            bool result = DevtoolsSupport.ShouldEnable(config, ServerType.Dev, tempDir, viteOnly: true, out string reason);

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
        bool result = DevtoolsSupport.ShouldEnable(null, ServerType.Dev, tempDir, viteOnly: true, out string reason);

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
}
