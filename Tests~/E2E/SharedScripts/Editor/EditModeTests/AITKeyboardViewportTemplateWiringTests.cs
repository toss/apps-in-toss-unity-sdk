// -----------------------------------------------------------------------
// AITKeyboardViewportTemplateWiringTests.cs
// 소프트 키보드 뷰포트 고정 스크립트의 index.html 배선 가드.
//
// iOS WKWebView는 소프트 키보드가 뜨면 Unity의 키보드 입력창(position:fixed;
// bottom:0)을 드러내려고 문서를 스크롤하고, 그때 position:fixed 인
// #unity-container 가 함께 위로 끌려가 상단 UI가 화면 밖으로 밀린다.
// 템플릿은 visualViewport 이벤트마다 컨테이너를 보이는 영역에 다시 맞춰 이를
// 막는다. 실제 키보드 동작은 기기에서만 확인할 수 있으므로, 여기서는 그 계약
// (스크립트 존재·위치·복원 로직·CSS 전제)이 텍스트 수준에서 깨지지 않는지만
// Unity를 띄우지 않고 상시 검증한다.
// -----------------------------------------------------------------------

using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using AppsInToss;
using AppsInToss.Editor;

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
    public void IndexHtml_PinsUnityContainerTopAndHeightToVisualViewport()
    {
        string script = ExtractKeyboardViewportScript();

        Assert.IsTrue(
            script.Contains("getElementById('unity-container')"),
            "키보드 뷰포트 스크립트가 #unity-container 를 대상으로 하지 않습니다.");
        Assert.IsTrue(
            Regex.IsMatch(script, @"style\.top\s*=\s*Math\.round\(\s*vv\.offsetTop\s*\)"),
            "컨테이너 top 을 visualViewport.offsetTop 으로 고정하는 코드가 없습니다.");
        Assert.IsTrue(
            Regex.IsMatch(script, @"style\.height\s*=\s*Math\.round\(\s*vv\.height\s*\)"),
            "컨테이너 height 를 visualViewport.height 로 고정하는 코드가 없습니다.");
    }

    [Test]
    public void IndexHtml_ClearsInlineStylesWhenKeyboardHidden()
    {
        string script = ExtractKeyboardViewportScript();

        Assert.IsTrue(
            Regex.IsMatch(script, @"style\.top\s*=\s*''"),
            "키보드가 내려간 뒤 컨테이너 top 인라인 스타일을 지우지 않습니다. " +
            "px 값을 남기면 회전 등 이후 리사이즈에서 낡은 값이 그대로 남습니다.");
        Assert.IsTrue(
            Regex.IsMatch(script, @"style\.height\s*=\s*''"),
            "키보드가 내려간 뒤 컨테이너 height 인라인 스타일을 지우지 않습니다.");
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
            "#unity-container 는 position:fixed 여야 합니다. 키보드 뷰포트 스크립트가 top/height 인라인 " +
            "오버라이드로 보이는 영역에 고정하는 전제입니다.");
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
