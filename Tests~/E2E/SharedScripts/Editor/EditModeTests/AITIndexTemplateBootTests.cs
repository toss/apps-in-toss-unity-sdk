// -----------------------------------------------------------------------
// AITIndexTemplateBootTests.cs - index.html 부팅 크리티컬 패스 회귀 테스트
// Level 0: WebGLTemplates/AITTemplate/index.html 원본 소스를 텍스트 수준에서 검증
//  · style.css <link> 미존재 (CSSOM 블로킹으로 조기 fetch 인라인 스크립트 실행 지연 방지, 파일 자체는 보존)
//  · framework.js <link rel="preload">가 crossorigin 없이 <head>에 존재
//    (로더가 no-cors <script src>로 소비하므로 crossorigin 부여 시 이중 다운로드)
//  · waitForLoader()가 setInterval 등록 전에 동기 선검사로 즉시 콜백하는 경로를 가짐
// -----------------------------------------------------------------------

using System.IO;
using NUnit.Framework;
using AppsInToss;
using AppsInToss.Editor;

[TestFixture]
[Category("Unit")]
public class AITIndexTemplateBootTests
{
    private static string ReadIndexHtmlSource()
    {
        Assert.IsTrue(
            AITPackagePathResolver.TryResolveFile(
                "WebGLTemplates/AITTemplate/index.html",
                out string path,
                typeof(AITConvertCore)),
            "WebGLTemplates/AITTemplate/index.html 경로를 찾지 못했습니다.");
        Assert.IsTrue(File.Exists(path), $"파일이 존재하지 않습니다: {path}");
        return File.ReadAllText(path);
    }

    [Test]
    public void Head_DoesNotLinkDeadStyleSheet()
    {
        string html = ReadIndexHtmlSource();

        // 죽은 참조(TemplateData/style.css)를 <link rel="stylesheet">로 로드하지 않아야 한다 —
        // 존재하면 CSSOM 블로킹으로 뒤따르는 %AIT_PAGE_CACHE_SCRIPT%/%AIT_EARLY_FETCH_SCRIPT%
        // 인라인 스크립트의 '실행'이 지연되어 조기 fetch 킥오프가 최대 1 RTT 늦어진다.
        StringAssert.DoesNotContain("href=\"TemplateData/style.css\"", html);
    }

    [Test]
    public void StyleCssFile_StillExistsButIsDeadReference()
    {
        // 파일 자체는 삭제하지 않는다 — 링크만 제거한다(요구사항).
        Assert.IsTrue(
            AITPackagePathResolver.TryResolveFile(
                "WebGLTemplates/AITTemplate/TemplateData/style.css",
                out string path,
                typeof(AITConvertCore)),
            "TemplateData/style.css 파일이 삭제되어서는 안 됩니다.");
        Assert.IsTrue(File.Exists(path));
    }

    [Test]
    public void Head_HasFrameworkPreload_WithoutCrossOrigin()
    {
        string html = ReadIndexHtmlSource();

        int headStart = html.IndexOf("<head>");
        int headEnd = html.IndexOf("</head>");
        Assert.GreaterOrEqual(headStart, 0, "<head> 태그를 찾을 수 없음");
        Assert.Greater(headEnd, headStart, "</head> 태그를 찾을 수 없음");
        string head = html.Substring(headStart, headEnd - headStart);

        // framework.js는 t≈0에 프리로드 스캐너가 보도록 <head>에 리터럴 <link rel="preload">가 있어야 한다.
        StringAssert.Contains("rel=\"preload\"", head);
        StringAssert.Contains("as=\"script\"", head);
        StringAssert.Contains("href=\"%UNITY_WEBGL_FRAMEWORK_URL%\"", head);
        StringAssert.Contains("fetchpriority=\"high\"", head);

        // 로더가 framework.js를 crossorigin 없는 <script src>(no-cors)로 소비하므로,
        // preload에 crossorigin을 붙이면 fetch 모드 불일치로 이중 다운로드가 발생한다.
        int preloadIdx = head.IndexOf("rel=\"preload\"");
        int preloadTagStart = head.LastIndexOf('<', preloadIdx);
        int preloadTagEnd = head.IndexOf('>', preloadIdx);
        Assert.GreaterOrEqual(preloadTagStart, 0);
        Assert.Greater(preloadTagEnd, preloadTagStart);
        string preloadTag = head.Substring(preloadTagStart, preloadTagEnd - preloadTagStart + 1);
        StringAssert.DoesNotContain("crossorigin", preloadTag);
    }

    [Test]
    public void Head_DoesNotPreloadDataOrCodeUrl()
    {
        string html = ReadIndexHtmlSource();
        int headStart = html.IndexOf("<head>");
        int headEnd = html.IndexOf("</head>");
        string head = html.Substring(headStart, headEnd - headStart);

        // data/wasm은 CDN max-age=0 환경에서 preload 시 이중 다운로드 위험이 있어 대상에서 제외한다
        // (WebGLBuildCopier.GenerateEarlyFetchScript 주석의 근거와 동일).
        StringAssert.DoesNotContain("%UNITY_WEBGL_DATA_URL%", head);
        StringAssert.DoesNotContain("%UNITY_WEBGL_CODE_URL%", head);
    }

    [Test]
    public void WaitForLoader_HasSynchronousPreCheck_BeforeSetInterval()
    {
        string html = ReadIndexHtmlSource();

        int fnIdx = html.IndexOf("function waitForLoader(callback, timeoutMs) {");
        Assert.GreaterOrEqual(fnIdx, 0, "waitForLoader 함수 선언을 찾을 수 없음");
        int intervalIdx = html.IndexOf("setInterval(function()", fnIdx);
        Assert.Greater(intervalIdx, fnIdx, "waitForLoader 내부 setInterval 호출을 찾을 수 없음");

        string preBody = html.Substring(fnIdx, intervalIdx - fnIdx);

        // 성공 경로(로더가 이미 동기 로드 완료)에서 setInterval의 첫 tick(최대 50ms)을
        // 기다리지 않도록, 동일 조건을 setInterval 등록 전에 동기적으로 한 번 검사해야 한다.
        StringAssert.Contains("window._aitLoaderLoaded", preBody);
        StringAssert.Contains("typeof createUnityInstance !== 'undefined'", preBody);
        StringAssert.Contains("callback(true)", preBody);
        StringAssert.Contains("return;", preBody);
    }
}
