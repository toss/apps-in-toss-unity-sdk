// -----------------------------------------------------------------------
// AITGlHookTemplateTests.cs - ait-gl.js(WebGL 컨텍스트 레이어)의 템플릿 배선과 계약 검증
//   · ait-gl.js 스크립트 태그가 "Unity Loader Script" 앞에 있다(getContext 훅이 컨텍스트 생성보다 먼저 설치돼야 우회되지 않음)
//   · ait-gl.js 가 스텁이 아니고, 크로스 파일 계약 이름(__AIT_GL.tierCap/bindContextLoss/contexts, __ait_gl_tier 등)을 담고 있다
//   · ait-gl.js 가 읽는 __AIT_PERF 키가 AITPerfFlags.ToJson 이 내보내는 키와 일치한다
//   · webglcontextlost 에서 preventDefault 를 호출하지 않는다(복구는 reload)
//   · probe 해제는 WEBGL_lose_context 로만, Unity 캔버스 보호 조건이 코드에 있다
// -----------------------------------------------------------------------

using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using AppsInToss;          // AITConvertCore (namespace AppsInToss)
using AppsInToss.Editor;

[TestFixture]
[Category("Unit")]
public class AITGlHookTemplateTests
{
    private const string LoaderMarker = "<!-- Unity Loader Script -->";

    [Test]
    public void IndexHtml_IncludesAitGl_BeforeUnityLoaderScript()
    {
        string html = ReadPackageFile("WebGLTemplates/AITTemplate/index.html");

        int marker = html.IndexOf(LoaderMarker, System.StringComparison.Ordinal);
        int include = html.IndexOf("<script src=\"Runtime/ait-gl.js\"", System.StringComparison.Ordinal);

        Assert.GreaterOrEqual(marker, 0, "index.html 에 '" + LoaderMarker + "' 마커가 없습니다.");
        Assert.GreaterOrEqual(include, 0, "index.html 에 ait-gl.js 스크립트 태그가 없습니다.");
        Assert.Less(include, marker, "ait-gl.js 는 Unity 로더(= WebGL 컨텍스트 생성)보다 먼저 로드돼야 합니다.");
        Assert.AreEqual(1, Regex.Matches(html, Regex.Escape("Runtime/ait-gl.js")).Count,
            "ait-gl.js 참조는 정확히 한 번이어야 합니다(주석에도 파일 경로를 반복해 적지 않습니다).");
    }

    [Test]
    public void AitGl_IsNotAStub_AndDefinesContractNames()
    {
        string js = ReadAitGl();

        StringAssert.DoesNotContain("__stub", js, "ait-gl.js 가 아직 스텁입니다.");
        foreach (string name in new[]
        {
            "window.__AIT_GL", "tierCap", "bindContextLoss", "contexts",
            "HTMLCanvasElement.prototype", "getContext",
            "webglcontextlost", "WEBGL_lose_context",
            "__ait_gl_tier", "__ait_gl_loss__",
            "AITMemory", "crashCount",
            "aitglprobe", "renderbufferStorageMultisample",
            "[AIT-GL]",
        })
        {
            StringAssert.Contains(name, js, "ait-gl.js 에 계약 이름 '" + name + "' 이 없습니다.");
        }
    }

    [Test]
    public void AitGl_ReadsOnlyPerfKeys_ThatAITPerfFlagsEmits()
    {
        string js = ReadAitGl();
        string flags = ReadPackageFile("Editor/Package/AITPerfFlags.cs");

        foreach (string key in new[] { "glHook", "glDropAntialias", "glContextRecovery" })
        {
            Assert.IsTrue(Regex.IsMatch(js, "flag\\('" + key + "'"), "ait-gl.js 가 __AIT_PERF." + key + " 를 읽지 않습니다.");
            StringAssert.Contains("\"" + key + "\"", flags, "AITPerfFlags.ToJson 이 " + key + " 키를 내보내지 않습니다.");
        }
    }

    [Test]
    public void AitGl_PerfFlagDefaults_AreFailOpen()
    {
        string js = ReadAitGl();

        // 키가 없을 때의 기본값: 훅 on, antialias 해제 off, 복구 on.
        StringAssert.Contains("flag('glHook', true)", js);
        StringAssert.Contains("flag('glDropAntialias', false)", js);
        StringAssert.Contains("flag('glContextRecovery', true)", js);
    }

    [Test]
    public void AitGl_ContextLoss_DoesNotPreventDefault()
    {
        string js = ReadAitGl();

        Assert.IsFalse(Regex.IsMatch(js, @"\.preventDefault\s*\("),
            "webglcontextlost 에서 preventDefault 를 호출하면 컨텍스트가 복원 대기 상태로 남아 reload 복구가 일어나지 않습니다.");
        StringAssert.Contains("location.reload()", js);
    }

    [Test]
    public void AitGl_ProbeRelease_NeverTouchesTheUnityCanvas()
    {
        string js = ReadAitGl();

        // 해제 경로는 WEBGL_lose_context 하나뿐이고, Unity 캔버스/DOM 에 붙은 캔버스를 걸러내는 판정이 있어야 한다.
        StringAssert.Contains("'unity-canvas'", js);
        StringAssert.Contains("isConnected", js);
        Assert.AreEqual(1, Regex.Matches(js, @"\.loseContext\s*\(").Count,
            "loseContext 호출은 releaseProbe 안의 한 곳뿐이어야 합니다.");

        Match release = Regex.Match(js, @"function releaseProbe\([^)]*\)\s*\{(?<body>.*?)\r?\n    \}\r?\n", RegexOptions.Singleline);
        Assert.IsTrue(release.Success, "releaseProbe 함수를 찾지 못했습니다.");
        string body = release.Groups["body"].Value;
        StringAssert.Contains("isUnityCanvas(canvas)", body);
        StringAssert.Contains("isAttached(canvas)", body);
        StringAssert.Contains("loseContext", body);
    }

    [Test]
    public void AitGl_Tier_UsesTwentyFourHourExpiry_AndSteps()
    {
        string js = ReadAitGl();

        StringAssert.Contains("24 * 60 * 60 * 1000", js);
        StringAssert.Contains("[2, 1.5, 1]", js);
    }

    [Test]
    public void AitGl_KoreanOverlayAndLogs_ArePresent()
    {
        string js = ReadAitGl();

        StringAssert.Contains("다시 시도", js);
        StringAssert.Contains("앱을 완전히 종료한 뒤", js);
    }

    [Test]
    public void GlFixtureVariants_AreRegisteredInVariantRegistry()
    {
        // Apply 는 프로젝트 QualitySettings 를 바꾸므로 호출하지 않고 등록 여부만 본다.
        foreach (string name in new[] { "gl-aa4", "gl-aa4-drop", "gl-aa4-stock" })
        {
            Assert.IsTrue(HeavyBuildVariants.IsRegistered(name), "GL 픽스처 변형이 등록되지 않았습니다: " + name);
        }
    }

    private static string ReadAitGl()
    {
        return ReadPackageFile("WebGLTemplates/AITTemplate/Runtime/ait-gl.js");
    }

    private static string ReadPackageFile(string relativePath)
    {
        Assert.IsTrue(
            AITPackagePathResolver.TryResolveFile(relativePath, out string path, typeof(AITConvertCore)),
            relativePath + " 경로를 찾지 못했습니다.");
        Assert.IsTrue(File.Exists(path), "파일이 존재하지 않습니다: " + path);
        return File.ReadAllText(path);
    }
}
