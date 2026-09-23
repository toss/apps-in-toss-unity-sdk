// -----------------------------------------------------------------------
// AITKeyboardViewportTemplateWiringTests.cs
// 소프트 키보드 뷰포트 고정 스크립트의 index.html 배선 가드.
//
// iOS WKWebView는 소프트 키보드가 뜨면 Unity의 키보드 입력창(position:fixed;
// bottom:0)을 드러내려고 문서를 스크롤하고, 그때 position:fixed 인
// #unity-container 가 함께 위로 끌려가 상단 UI가 화면 밖으로 밀린다.
// 템플릿은 visualViewport 이벤트마다 컨테이너의 top 만 보이는 영역에 다시
// 맞춰 이를 막는다. height 는 절대 건드리지 않는다 — 캔버스 크기가 바뀌면
// Unity 가 프레임버퍼를 다시 만들고 UI 전체를 리레이아웃하는데, 키보드
// 애니메이션 동안 프레임마다 그 비용이 들어 화면이 끊긴다. 키보드에 가려지는
// 아래쪽 영역은 그냥 보이지 않는 채로 둔다(네이티브 adjustPan 과 같은 동작).
// 실제 키보드 동작은 기기에서만 확인할 수 있으므로, 여기서는 그 계약
// (스크립트 존재·위치·복원 로직·CSS 전제·height 미변경)이 텍스트 수준에서
// 깨지지 않는지만 Unity를 띄우지 않고 상시 검증한다.
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
    public void IndexHtml_NeverResizesUnityContainerHeight()
    {
        string script = ExtractKeyboardViewportScript();

        Assert.IsFalse(
            Regex.IsMatch(script, @"style\.height"),
            "키보드 뷰포트 스크립트가 #unity-container 의 height 를 건드립니다. " +
            "캔버스 크기가 바뀌면 Unity 가 프레임버퍼를 다시 만들고 UI 전체를 리레이아웃하는데, " +
            "키보드 애니메이션 동안 프레임마다 그 비용이 들어 화면이 끊깁니다. height 는 절대 건드리지 않아야 합니다.");
        Assert.IsFalse(
            Regex.IsMatch(script, @"vv\.height"),
            "키보드 뷰포트 스크립트가 visualViewport.height 를 참조합니다. " +
            "캔버스 리사이즈를 유발하는 값이므로(프레임버퍼 재생성 → 키보드 애니메이션 중 프레임마다 버벅임) " +
            "이 스크립트에서는 offsetTop 만 사용해야 합니다.");
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
