// -----------------------------------------------------------------------
// AITPrewarmTests.cs - 프리웜 모드 설정·치환·템플릿 배선 검증
// Level 0: AssetDatabase 비의존 순수 로직과 소스 텍스트 가드
//   - EffectivePrewarm: null/tri-state 해석 (기본 OFF 기능이라 null → false)
//   - 템플릿: 플레이스홀더가 fail-closed 로 평가되고, 스크립트 로드 순서·begin()·엔진 시작 게이트 배선이 유지된다
//   - ait-warm.html(폐기) 산출 경로가 되살아나지 않았다
// -----------------------------------------------------------------------

using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using AppsInToss;
using AppsInToss.Editor;
using AppsInToss.Editor.Package;

[TestFixture]
[Category("Unit")]
public class AITPrewarmTests
{
    private AITEditorScriptObject _config;

    [SetUp]
    public void SetUp()
    {
        _config = ScriptableObject.CreateInstance<AITEditorScriptObject>();
    }

    [TearDown]
    public void TearDown()
    {
        if (_config != null)
        {
            Object.DestroyImmediate(_config);
            _config = null;
        }
    }

    // =====================================================
    // 1) EffectivePrewarm — null 은 false(기존 동작), tri-state 해석
    // =====================================================

    [Test]
    public void EffectivePrewarm_NullConfig_ReturnsFalse_FailClosed()
    {
        Assert.IsFalse(WebGLBuildCopier.EffectivePrewarm(null),
            "설정 로드 실패 시 프리웜은 꺼져야 합니다(기본 OFF 기능 — 기존 동작이 안전한 쪽).");
    }

    [Test]
    public void DefaultPrewarm_IsOff()
    {
        Assert.IsFalse(AITDefaultSettings.GetDefaultPrewarm(), "프리웜은 호스트 연동이 있어야 의미가 있는 opt-in 기능이라 기본 OFF 입니다.");
        Assert.AreEqual(-1, _config.prewarm, "신규 설정 필드의 선언 기본값은 -1(자동)이어야 합니다.");
    }

    [TestCase(-1, false, "-1(자동)은 GetDefaultPrewarm()==false 와 일치해야 합니다.")]
    [TestCase(0, false, "0 은 명시적 비활성입니다.")]
    [TestCase(1, true, "1 은 명시적 활성입니다.")]
    public void EffectivePrewarm_TriState_ReturnsExpected(int storedValue, bool expected, string reason)
    {
        _config.prewarm = storedValue;
        Assert.AreEqual(expected, WebGLBuildCopier.EffectivePrewarm(_config), reason);
    }

    // =====================================================
    // 2) 템플릿 배선
    // =====================================================

    [Test]
    public void IndexHtml_PrewarmPlaceholder_EvaluatesFalse_WhenUnsubstituted()
    {
        string html = ReadPackageFile("WebGLTemplates/AITTemplate/index.html");

        // 미치환 리터럴('%AIT_PREWARM%')은 'true' 가 아니므로 false — 'false' 와 비교하는 fail-open 방식이면 안 된다.
        Assert.IsTrue(
            Regex.IsMatch(html, @"window\.__AIT_PREWARM_ENABLED\s*=\s*'%AIT_PREWARM%'\s*===\s*'true'\s*;"),
            "index.html 이 window.__AIT_PREWARM_ENABLED = '%AIT_PREWARM%' === 'true' 로 선언해야 미치환일 때 꺼집니다.");
    }

    [Test]
    public void IndexHtml_LoadsPrewarmScript_AfterDataBuf_BeforeUnityLoader()
    {
        string html = ReadPackageFile("WebGLTemplates/AITTemplate/index.html");

        int databuf = html.IndexOf("<script src=\"Runtime/ait-databuf.js\"", System.StringComparison.Ordinal);
        int prewarm = html.IndexOf("<script src=\"Runtime/ait-prewarm.js\"", System.StringComparison.Ordinal);
        int loader = html.IndexOf("<script src=\"%UNITY_WEBGL_LOADER_URL%\"", System.StringComparison.Ordinal);

        Assert.GreaterOrEqual(prewarm, 0, "index.html 에 ait-prewarm.js 스크립트 태그가 없습니다.");
        Assert.Less(databuf, prewarm, "ait-prewarm.js 는 ait-databuf.js 다음에 로드합니다.");
        Assert.Less(prewarm, loader, "ait-prewarm.js 는 Unity 로더보다 먼저 로드돼야 합니다.");
        Assert.AreEqual(1, Regex.Matches(html, Regex.Escape("Runtime/ait-prewarm.js")).Count,
            "ait-prewarm.js 참조는 정확히 한 번이어야 합니다(주석에도 파일 경로를 반복해 적지 않습니다).");
    }

    [Test]
    public void IndexHtml_BeginPrecedesEngineStartGate_AndGateWrapsCreateUnityInstance()
    {
        string html = ReadPackageFile("WebGLTemplates/AITTemplate/index.html");

        int begin = html.IndexOf("__AIT_PREWARM.begin(config)", System.StringComparison.Ordinal);
        int gate = html.IndexOf("aitStartBoot(function () { waitForLoader(", System.StringComparison.Ordinal);
        int create = html.IndexOf("createUnityInstance(canvas, config", System.StringComparison.Ordinal);
        int gateEnd = html.IndexOf("}, 10000); }); // waitForLoader", System.StringComparison.Ordinal);

        Assert.GreaterOrEqual(begin, 0, "index.html 에 __AIT_PREWARM.begin(config) 호출이 없습니다.");
        Assert.GreaterOrEqual(gate, 0, "엔진 시작 블록이 aitStartBoot 게이트로 감싸져 있지 않습니다.");
        Assert.Less(begin, gate, "begin() 은 fetch 래퍼가 모두 설치된 뒤, 엔진 시작 게이트보다 먼저 불려야 합니다.");
        Assert.Less(gate, create, "createUnityInstance 호출은 게이트 안에 있어야 합니다.");
        Assert.Less(create, gateEnd, "createUnityInstance 호출은 게이트 안에 있어야 합니다.");
    }

    [Test]
    public void IndexHtml_InstantiateWasm_UsesPrewarmedModule_WithFallbackToNormalPath()
    {
        string html = ReadPackageFile("WebGLTemplates/AITTemplate/index.html");

        Assert.IsTrue(html.Contains("__AIT_PREWARM.takeModule()"), "instantiateWasm 이 선컴파일 Module 을 받아야 합니다.");
        Assert.IsTrue(html.Contains("WebAssembly.instantiate(module, imports)"), "선컴파일 Module 은 instantiate(module, imports) 로 인스턴스화해야 합니다.");
        Assert.IsTrue(Regex.IsMatch(html, @"if \(!module\) \{ startFetch\(\); return; \}"), "선컴파일이 실패(null)하면 기존 경로(startFetch)로 폴백해야 합니다.");
    }

    // =====================================================
    // 3) 폐기된 ait-warm.html 경로가 없다
    // =====================================================

    [Test]
    public void WarmPageEmitter_IsRemoved()
    {
        Assert.IsFalse(AITPackagePathResolver.TryResolveFile("Editor/Package/AITWarmPageEmitter.cs", out string path, typeof(AITConvertCore)) && File.Exists(path),
            "ait-warm.html 산출기는 프리웜으로 대체되어 제거됐습니다.");
        string copier = ReadPackageFile("Editor/Package/WebGLBuildCopier.cs");
        Assert.IsFalse(copier.Contains("AITWarmPageEmitter"), "WebGLBuildCopier 가 제거된 산출기를 참조하면 안 됩니다.");
    }

    private static string ReadPackageFile(string relative)
    {
        Assert.IsTrue(
            AITPackagePathResolver.TryResolveFile(relative, out string path, typeof(AITConvertCore)),
            $"{relative} 경로를 찾지 못했습니다.");
        Assert.IsTrue(File.Exists(path), $"파일이 존재하지 않습니다: {path}");
        return File.ReadAllText(path);
    }
}
