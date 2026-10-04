// -----------------------------------------------------------------------
// AITKeyboardViewportTemplateWiringTests.cs
// 소프트 키보드 뷰포트 고정 스크립트의 index.html 배선 가드.
//
// iOS WKWebView는 소프트 키보드가 뜨면 Unity의 키보드 입력창(position:fixed;
// bottom:0)을 드러내려고 문서를 스크롤하고, 그때 position:fixed 인
// #unity-container 가 함께 위로 끌려가 상단 UI가 화면 밖으로 밀린다.
// 템플릿은 빌드 시 치환되는 모드(pan 기본 / resize / none)에 따라 동작한다.
// pan·none 은 height 를 절대 건드리지 않는다 — 캔버스 크기가 바뀌면 Unity 가
// 프레임버퍼를 다시 만들고 UI 전체를 리레이아웃하는데, 키보드 애니메이션 동안
// 프레임마다 그 비용이 들어 화면이 끊긴다. pan 은 탭한 입력창이 가릴 때만
// transform 으로 화면을 올린다(네이티브 adjustPan 과 같은 동작).
// 실제 키보드 동작은 기기에서만 확인할 수 있으므로, 여기서는 그 계약이
// 텍스트 수준에서 깨지지 않는지만 Unity를 띄우지 않고 상시 검증한다.
// -----------------------------------------------------------------------

using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using AppsInToss;
using AppsInToss.Editor;
using AppsInToss.Editor.Package;

[TestFixture]
[Category("Unit")]
public class AITKeyboardViewportTemplateWiringTests
{
    [Test]
    public void IndexHtml_ListensToVisualViewportResizeAndScroll()
    {
        string script = ExtractKeyboardViewportScript();

        Assert.IsTrue(
            Regex.IsMatch(script, @"addEventListener\(\s*'resize'"),
            "키보드 뷰포트 스크립트가 visualViewport 'resize' 이벤트를 구독하지 않습니다.");
        Assert.IsTrue(
            Regex.IsMatch(script, @"addEventListener\(\s*'scroll'"),
            "키보드 뷰포트 스크립트가 visualViewport 'scroll' 이벤트를 구독하지 않습니다. " +
            "iOS는 키보드가 뜬 뒤 스크롤만으로도 offsetTop이 바뀌므로 둘 다 필요합니다.");
    }

    [Test]
    public void IndexHtml_SetsUnityContainerTopFromVisualViewportOffset()
    {
        string script = ExtractKeyboardViewportScript();

        Assert.IsTrue(
            script.Contains("getElementById('unity-container')"),
            "키보드 뷰포트 스크립트가 #unity-container 를 대상으로 하지 않습니다.");
        Assert.IsTrue(
            Regex.IsMatch(script, @"offset\s*=\s*Math\.round\(\s*vv\.offsetTop\s*\)"),
            "visualViewport.offsetTop 을 반올림해 offset 을 구하는 코드가 없습니다.");
        Assert.IsTrue(
            Regex.IsMatch(script, @"style\.top\s*=\s*offset"),
            "컨테이너 top 을 offset(visualViewport.offsetTop) 으로 맞추는 코드가 없습니다.");
    }

    [Test]
    public void IndexHtml_ClearsInlineTopWhenKeyboardHidden()
    {
        string script = ExtractKeyboardViewportScript();

        Assert.IsTrue(
            Regex.IsMatch(script, @"offset\s*>\s*1\s*\?[^:]*:\s*''"),
            "offset(visualViewport.offsetTop) 이 1 이하로 돌아왔을 때 컨테이너 top 인라인 스타일을 " +
            "지우는 삼항식이 없습니다. px 값을 남기면 회전 등 이후 리사이즈에서 낡은 값이 그대로 남습니다.");
    }

    [Test]
    public void IndexHtml_NoneMode_NeverTouchesContainerHeight()
    {
        string script = ExtractKeyboardViewportScript();

        foreach (string fn in new[] { "applyTop", "applyNone" })
        {
            string body = ExtractFunction(script, fn);
            Assert.IsFalse(
                Regex.IsMatch(body, @"style\.height"),
                $"{fn} 가 #unity-container 의 height 를 건드립니다. 캔버스 크기가 바뀌면 Unity 가 프레임버퍼를 다시 만들고 " +
                "UI 전체를 리레이아웃하므로 키보드 애니메이션 동안 프레임마다 끊깁니다.");
        }
    }

    [Test]
    public void IndexHtml_PanMode_PinsContainerHeightOnlyWhenWebViewShrinks()
    {
        string script = ExtractKeyboardViewportScript();
        string pan = ExtractFunction(script, "applyPan");

        // Android WebView 는 키보드만큼 WebView 를 줄여 innerHeight 가 함께 준다. 키보드 높이는 그 폭에서 본 최대 높이 기준이어야 한다
        Assert.IsTrue(Regex.IsMatch(ExtractFunction(script, "keyboardUp"), @"fullHeight\(\)\s*-\s*vv\.height"),
            "keyboardUp 이 innerHeight 기준이면 Android 에서 키보드를 감지하지 못합니다(innerHeight 와 vv.height 가 함께 준다).");
        Assert.IsTrue(Regex.IsMatch(ExtractFunction(script, "fullHeight"), @"window\.innerWidth\s*!==\s*baseW"),
            "기준 높이를 폭(회전)이 바뀔 때 다시 잡지 않습니다.");
        Assert.IsTrue(
            Regex.IsMatch(pan, @"style\.height\s*=\s*up\s*&&\s*isTextField\(document\.activeElement\)\s*&&\s*window\.innerHeight\s*<\s*full\s*-\s*1\s*\?\s*\(baseCH\s*\|\|\s*full\)\s*\+\s*'px'\s*:\s*''"),
            "pan 모드는 키보드가 떠 있고 입력창에 포커스가 있으며 WebView 가 줄었을 때만 컨테이너 높이를 기준 높이로 고정하고, 그 밖에는 지워야 합니다.");
        Assert.IsTrue(
            Regex.IsMatch(pan, @"if\s*\(\s*!up\s*&&\s*!c\.style\.height\s*\)\s*baseCH\s*=\s*c\.getBoundingClientRect\(\)\.height"),
            "고정 높이는 키보드 없을 때 잰 컨테이너 높이(소수 포함)여야 합니다. 정수 innerHeight 로 고정하면 프레임버퍼가 1px 바뀝니다.");
    }

    [Test]
    public void IndexHtml_PlacesUnityInputBarAboveKeyboard_InAllModes()
    {
        string script = ExtractKeyboardViewportScript();

        // Unity 의 _JS_MobileKeyboard_Show 는 body 에 position:fixed; bottom:0 입력 바를 붙인다. iOS 는 레이아웃 뷰포트가
        // 줄지 않아 그대로 두면 바가 키보드 뒤에 가려진다
        Assert.IsTrue(Regex.IsMatch(ExtractFunction(script, "unityInputBar"), @"\.okButton\s*&&[^)]*\.input"),
            "Unity 입력 바를 okButton·input 속성으로 찾지 않습니다.");
        Assert.IsTrue(
            Regex.IsMatch(ExtractFunction(script, "placeInputBar"), @"window\.innerHeight\s*-\s*vv\.offsetTop\s*-\s*vv\.height"),
            "입력 바를 보이는 영역 맨 아래(innerHeight - offsetTop - vv.height)로 옮기지 않습니다.");
        string apply = ExtractFunction(script, "apply");
        int barIndex = apply.IndexOf("placeInputBar(bar)", System.StringComparison.Ordinal);
        int modeIndex = apply.IndexOf("if (mode === 'resize')", System.StringComparison.Ordinal);
        Assert.GreaterOrEqual(barIndex, 0, "apply() 가 입력 바 위치를 잡지 않습니다.");
        Assert.Less(barIndex, modeIndex, "입력 바 위치는 모드 분기 전에, 모든 모드에서 잡아야 합니다.");
        int earlyReturn = script.IndexOf("if (mode !== 'pan') return;", System.StringComparison.Ordinal);
        int barFocusin = script.IndexOf("addEventListener('focusin'", System.StringComparison.Ordinal);
        Assert.Less(barFocusin, earlyReturn, "입력 바가 생길 때의 focusin 구독은 pan 전용 분기보다 앞에 있어야 합니다.");
        Assert.IsTrue(ExtractFunction(script, "applyPan").Contains("bar.offsetHeight"),
            "pan 이동량에 입력 바 높이를 더하지 않으면 아래쪽 입력창이 바에 가려집니다.");
    }

    [Test]
    public void IndexHtml_ResizeMode_SetsTopAndHeightFromVisualViewport()
    {
        string body = ExtractFunction(ExtractKeyboardViewportScript(), "applyResize");

        Assert.IsTrue(Regex.IsMatch(body, @"style\.top\s*=\s*Math\.round\(\s*vv\.offsetTop"), "resize 모드가 top 을 vv.offsetTop 으로 맞추지 않습니다.");
        Assert.IsTrue(Regex.IsMatch(body, @"style\.height\s*=\s*Math\.round\(\s*vv\.height"), "resize 모드가 height 를 vv.height 로 맞추지 않습니다.");
        Assert.IsTrue(Regex.IsMatch(body, @"style\.height\s*=\s*''"), "resize 모드가 키보드가 내려갔을 때 height 인라인 스타일을 지우지 않습니다.");
    }

    [Test]
    public void IndexHtml_PanMode_UsesTransformAndClearsWhenKeyboardHidden()
    {
        string body = ExtractFunction(ExtractKeyboardViewportScript(), "applyPan");

        Assert.IsTrue(Regex.IsMatch(body, @"style\.transform\s*=\s*pan\s*>\s*0\s*\?\s*'translateY\("), "pan 모드가 transform:translateY 로 화면을 올리지 않습니다.");
        Assert.IsTrue(Regex.IsMatch(body, @"style\.transform\s*=[^;]*:\s*''"), "pan 모드가 pan 이 0 일 때(키보드 내려감 포함) transform 을 지우지 않습니다.");
        Assert.IsTrue(body.Contains("keyboardUp()") && body.Contains("focusY"), "pan 모드는 키보드가 떠 있고 탭 위치(focusY)가 있을 때만 이동해야 합니다.");
        Assert.IsTrue(Regex.IsMatch(body, @"transition\s*=\s*'transform "), "pan 이동에 transform 전용 transition 이 없습니다.");
    }

    [Test]
    public void IndexHtml_PanMode_TracksPointerAndFocusOfTextFields()
    {
        string script = ExtractKeyboardViewportScript();

        Assert.IsTrue(Regex.IsMatch(script, @"addEventListener\(\s*'pointerdown'"), "pointerdown 캡처 구독이 없습니다.");
        Assert.IsTrue(Regex.IsMatch(script, @"addEventListener\(\s*'touchstart'"), "touchstart 캡처 구독이 없습니다.");
        Assert.IsTrue(Regex.IsMatch(script, @"addEventListener\(\s*'focusin'"), "focusin 구독이 없습니다.");
        Assert.IsTrue(Regex.IsMatch(script, @"addEventListener\(\s*'focusout'"), "focusout 구독이 없습니다.");
        Assert.IsTrue(script.Contains("getBoundingClientRect().top"), "탭 좌표를 컨테이너 콘텐츠 좌표로 바꾸지 않습니다.");
        Assert.IsTrue(script.Contains("POINTER_FRESH_MS"), "오래된 탭 좌표를 걸러내는 기준(POINTER_FRESH_MS)이 없습니다.");
        Assert.IsTrue(script.Contains("PAN_MARGIN"), "PAN_MARGIN 상수가 없습니다.");
    }

    [Test]
    public void IndexHtml_PanMode_HandlesUnityReusedInputAndSilentRemoval()
    {
        string script = ExtractKeyboardViewportScript();
        string pan = ExtractFunction(script, "applyPan");

        // 키보드가 뜬 채 다른 입력창을 탭하면 Unity 는 같은 input 을 재사용해 focusin 이 없다
        Assert.IsTrue(script.Contains("FIELD_SWITCH_CHECK_MS"), "키보드가 뜬 채 입력창을 바꿀 때 탭 위치를 다시 잡는 처리(FIELD_SWITCH_CHECK_MS)가 없습니다.");
        Assert.IsTrue(Regex.IsMatch(script, @"document\.activeElement\s*===\s*el\s*&&\s*keyboardUp\(\)"), "전환 확인이 같은 input 의 포커스 유지와 키보드 유지를 함께 보지 않습니다.");
        // Unity 가 input 을 DOM 에서 지울 때 WebKit 은 focusout 을 보내지 않는다
        Assert.IsTrue(Regex.IsMatch(pan, @"if\s*\(\s*wasUp\s*&&\s*!up\s*\)\s*focusY\s*=\s*null"), "키보드가 내려갈 때 탭 위치를 지우지 않습니다.");
    }

    [Test]
    public void IndexHtml_KeyboardMode_PlaceholderFallsBackToPan()
    {
        string script = ExtractKeyboardViewportScript();

        Assert.IsTrue(script.Contains("var mode = '%AIT_KEYBOARD_MODE%'"), "스크립트에 %AIT_KEYBOARD_MODE% 플레이스홀더가 없습니다.");
        Assert.IsTrue(
            Regex.IsMatch(script, @"if\s*\(\s*mode\s*!==\s*'resize'\s*&&\s*mode\s*!==\s*'none'\s*\)\s*mode\s*=\s*'pan'"),
            "미치환 리터럴·알 수 없는 값이 pan 으로 떨어지는 fail-safe 가 없습니다.");
        Assert.IsTrue(
            Regex.IsMatch(script, @"if\s*\(\s*mode\s*===\s*'resize'\s*\)\s*applyResize\(c\);\s*else if\s*\(\s*mode\s*===\s*'none'\s*\)\s*applyNone\(c\);\s*else applyPan\(c,\s*bar\)"),
            "apply() 의 분기 기본값이 pan 이 아닙니다.");
    }

    [Test]
    public void WebGLBuildCopier_SubstitutesKeyboardModePlaceholder()
    {
        Assert.IsTrue(
            AITPackagePathResolver.TryResolveFile("Editor/Package/WebGLBuildCopier.cs", out string path, typeof(AITConvertCore)),
            "WebGLBuildCopier.cs 경로를 찾지 못했습니다.");
        Assert.IsTrue(
            File.ReadAllText(path).Contains("Replace(\"%AIT_KEYBOARD_MODE%\""),
            "WebGLBuildCopier.cs에 %AIT_KEYBOARD_MODE% 치환 코드가 없습니다.");
    }

    [Test]
    public void KeyboardModeToken_MapsSettingToPlaceholderValue_DefaultPan()
    {
        Assert.AreEqual("pan", WebGLBuildCopier.KeyboardModeToken(null), "config 가 null 이면 pan 이어야 합니다.");

        var config = UnityEngine.ScriptableObject.CreateInstance<AITEditorScriptObject>();
        try
        {
            Assert.AreEqual(AITKeyboardMode.Pan, config.keyboardMode, "필드 기본값(= 필드가 없던 기존 에셋)은 Pan 이어야 합니다.");
            Assert.AreEqual("pan", WebGLBuildCopier.KeyboardModeToken(config));
            config.keyboardMode = AITKeyboardMode.Resize;
            Assert.AreEqual("resize", WebGLBuildCopier.KeyboardModeToken(config));
            config.keyboardMode = AITKeyboardMode.None;
            Assert.AreEqual("none", WebGLBuildCopier.KeyboardModeToken(config));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(config);
        }
    }

    [Test]
    public void IndexHtml_KeyboardViewportScript_IsInHead_BeforeUnityLoader()
    {
        string html = ReadIndexHtml();

        int scriptIndex = html.IndexOf("window.visualViewport");
        int headEndIndex = html.IndexOf("</head>");
        int unityLoaderIndex = html.IndexOf("%UNITY_WEBGL_LOADER_URL%");

        Assert.GreaterOrEqual(scriptIndex, 0, "index.html에 window.visualViewport 를 쓰는 스크립트가 없습니다.");
        Assert.GreaterOrEqual(headEndIndex, 0, "index.html에 </head> 가 없습니다.");
        Assert.GreaterOrEqual(unityLoaderIndex, 0, "index.html에 %UNITY_WEBGL_LOADER_URL% 플레이스홀더가 없습니다.");
        Assert.Less(scriptIndex, headEndIndex, "키보드 뷰포트 스크립트는 <head> 안에 있어야 합니다.");
        Assert.Less(
            scriptIndex,
            unityLoaderIndex,
            "키보드 뷰포트 스크립트는 Unity 로더보다 먼저 실행되어 첫 키보드 표시부터 동작해야 합니다.");
    }

    [Test]
    public void IndexHtml_UnityContainer_StaysPositionFixed()
    {
        string html = ReadIndexHtml();

        var containerRule = Regex.Match(html, @"#unity-container\s*\{[^}]*\}", RegexOptions.Singleline);
        Assert.IsTrue(containerRule.Success, "index.html <style> 에 #unity-container 규칙이 없습니다.");
        Assert.IsTrue(
            Regex.IsMatch(containerRule.Value, @"position\s*:\s*fixed"),
            "#unity-container 는 position:fixed 여야 합니다. 키보드 뷰포트 스크립트가 top 인라인 " +
            "오버라이드만으로 보이는 영역에 맞추는 전제입니다.");
    }

    private static string ExtractKeyboardViewportScript()
    {
        string html = ReadIndexHtml();

        int start = html.IndexOf("window.visualViewport");
        Assert.GreaterOrEqual(start, 0, "index.html에 window.visualViewport 를 쓰는 스크립트가 없습니다.");
        int end = html.IndexOf("</script>", start);
        Assert.Greater(end, start, "키보드 뷰포트 스크립트의 </script> 닫는 태그를 찾지 못했습니다.");
        return html.Substring(start, end - start);
    }

    private static string ExtractFunction(string script, string name)
    {
        int start = script.IndexOf("function " + name + "(");
        Assert.GreaterOrEqual(start, 0, $"키보드 뷰포트 스크립트에 {name} 함수가 없습니다.");
        int next = script.IndexOf("\n            function ", start + 1);
        int end = next < 0 ? script.Length : next;
        return script.Substring(start, end - start);
    }

    private static string ReadIndexHtml()
    {
        Assert.IsTrue(
            AITPackagePathResolver.TryResolveFile(
                "WebGLTemplates/AITTemplate/index.html",
                out string path,
                typeof(AITConvertCore)),
            "index.html 경로를 찾지 못했습니다.");
        Assert.IsTrue(File.Exists(path), $"파일이 존재하지 않습니다: {path}");
        return File.ReadAllText(path);
    }
}
