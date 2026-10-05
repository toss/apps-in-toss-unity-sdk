// -----------------------------------------------------------------------
// AITLoadFailureTemplateWiringTests.cs
// index.html 의 빌드 파일 다운로드 실패 처리 배선 가드.
//
// 한 번의 로드에서 파일 여러 개가 연달아 실패하면 실패마다 재시도 예산을 써서
// 같은 로드 안에서 예산이 바닥나고 카운터가 초기화돼 reload 가 끝없이 반복됐다.
// 최종 오류 화면 뒤에도 정체 워치독이 새 사이클을 열었고, 오류 화면은
// position:fixed 인 캔버스 뒤에 가려져 보이지 않았다.
// -----------------------------------------------------------------------

using System.IO;
using NUnit.Framework;
using AppsInToss;
using AppsInToss.Editor;

[TestFixture]
[Category("Unit")]
public class AITLoadFailureTemplateWiringTests
{
    [Test]
    public void IndexHtml_DownloadWatchdog_HandlesOnlyFirstFailurePerLoad()
    {
        string html = ReadIndexHtml();

        Assert.IsTrue(html.Contains("&& !window._aitDownloadFailureHandled) {"),
            "다운로드 워치독이 한 로드에서 첫 실패만 처리하지 않습니다. 실패마다 예산을 쓰면 재시도가 끝나지 않습니다.");
        Assert.IsTrue(html.Contains("if (window._aitDownloadFailureHandled || window._aitLoadAborted) {"),
            ".catch 가 워치독이 맡은 다운로드 실패를 다시 처리해 재시도 카운터를 지웁니다.");
        Assert.IsTrue(html.Contains("if (window.unityInstance || window._aitLoadAborted) { clearInterval(_aitStallTimer); return; }"),
            "로드를 중단한 뒤에도 정체 워치독이 reload 할 수 있습니다.");
    }

    [Test]
    public void IndexHtml_ShowLoadError_IsTerminalAndCoversCanvas()
    {
        string html = ReadIndexHtml();

        int start = html.IndexOf("function showLoadError(");
        Assert.GreaterOrEqual(start, 0, "showLoadError 함수가 없습니다.");
        string body = html.Substring(start, html.IndexOf("wrapper.innerHTML", start) - start);

        Assert.IsTrue(body.Contains("window._aitLoadAborted = true;"),
            "오류 화면을 띄운 뒤에도 정체 워치독이 reload 사이클을 다시 시작합니다.");
        Assert.IsTrue(body.Contains("position: fixed") && body.Contains("z-index"),
            "오류 화면이 position:fixed 인 #unity-container 뒤에 가려져 사용자에게 보이지 않습니다.");
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
