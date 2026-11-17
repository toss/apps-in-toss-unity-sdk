// -----------------------------------------------------------------------
// AITPlayerPrefsTemplateWiringTests.cs
// WebGL PlayerPrefs → 앱인토스 Storage 영속화 레이어의 소스 텍스트 배선 가드.
//
// 이 레이어는 index.html / ait-playerprefs.js / WebGLBuildCopier.cs 세 파일에
// 걸친 순서·존재 계약(스크립트 로드 순서, 플레이스홀더 선언, configure() 호출
// 시점, 치환 체인 등)에 의존한다. 런타임 동작(syncfs 트랩, 스냅샷 복원 등)은
// Playwright E2E가 검증하지만, 이 계약 자체가 깨지는 회귀(예: 스크립트 태그
// 순서가 뒤바뀌거나 configure() 호출이 누락되는 경우)는 Unity를 띄우지 않는
// 텍스트 수준 가드로 저비용에 상시 검증한다.
// -----------------------------------------------------------------------

using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using AppsInToss;
using AppsInToss.Editor;

[TestFixture]
[Category("Unit")]
public class AITPlayerPrefsTemplateWiringTests
{
    [Test]
    public void IndexHtml_LoadsPlayerPrefsScript_BeforeUnityLoader()
    {
        string html = ReadIndexHtml();

        int playerPrefsScriptIndex = html.IndexOf("Runtime/ait-playerprefs.js");
        int unityLoaderIndex = html.IndexOf("%UNITY_WEBGL_LOADER_URL%");

        Assert.GreaterOrEqual(playerPrefsScriptIndex, 0, "index.html에 Runtime/ait-playerprefs.js 스크립트 태그가 없습니다.");
        Assert.GreaterOrEqual(unityLoaderIndex, 0, "index.html에 %UNITY_WEBGL_LOADER_URL% 플레이스홀더가 없습니다.");
        Assert.Less(
            playerPrefsScriptIndex,
            unityLoaderIndex,
            "ait-playerprefs.js는 Unity 로더 스크립트보다 먼저 로드되어야 스냅샷 fetch를 미리 착수할 수 있습니다.");
    }

    [Test]
    public void IndexHtml_DeclaresPlayerPrefsPlaceholder()
    {
        string html = ReadIndexHtml();

        Assert.IsTrue(
            html.Contains("%AIT_PLAYERPREFS_PERSISTENCE%"),
            "index.html에 %AIT_PLAYERPREFS_PERSISTENCE% 플레이스홀더 선언이 없습니다.");
    }

    [Test]
    public void IndexHtml_ConfigureCall_PrecedesCreateUnityInstance()
    {
        string html = ReadIndexHtml();

        int configureCallIndex = html.IndexOf("__AIT_PP.configure(config)");
        int createUnityInstanceIndex = html.IndexOf("createUnityInstance(canvas, config");

        Assert.GreaterOrEqual(configureCallIndex, 0, "index.html에 __AIT_PP.configure(config) 호출이 없습니다.");
        Assert.GreaterOrEqual(createUnityInstanceIndex, 0, "index.html에 createUnityInstance(canvas, config 호출이 없습니다.");
        Assert.Less(
            configureCallIndex,
            createUnityInstanceIndex,
            "__AIT_PP.configure(config)는 createUnityInstance보다 먼저 호출되어야 preRun 트랩이 설치됩니다.");
    }

    [Test]
    public void WebGLBuildCopier_SubstitutesPlayerPrefsPlaceholder()
    {
        string source = ReadCopierSource();

        Assert.IsTrue(
            source.Contains("Replace(\"%AIT_PLAYERPREFS_PERSISTENCE%\""),
            "WebGLBuildCopier.cs에 %AIT_PLAYERPREFS_PERSISTENCE% 치환 코드가 없습니다.");
    }

    [Test]
    public void PlayerPrefsScript_DoesNotAssignAppsInTossStorage()
    {
        string source = ReadPlayerPrefsScriptSource();

        var assignmentPattern = new Regex(@"window\.AppsInToss\.Storage\s*=");
        Assert.IsFalse(
            assignmentPattern.IsMatch(source),
            "ait-playerprefs.js가 window.AppsInToss.Storage에 직접 대입하고 있습니다. " +
            "unity-bridge.ts가 로드 시 이를 통째로 덮어쓰므로 절대 대입하면 안 됩니다 (오버라이드 훅 " +
            "window.__AIT_PLAYERPREFS_STORAGE__ 전역에만 설치해야 합니다).");
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

    private static string ReadPlayerPrefsScriptSource()
    {
        Assert.IsTrue(
            AITPackagePathResolver.TryResolveFile(
                "WebGLTemplates/AITTemplate/Runtime/ait-playerprefs.js",
                out string path,
                typeof(AITConvertCore)),
            "ait-playerprefs.js 경로를 찾지 못했습니다.");
        Assert.IsTrue(File.Exists(path), $"파일이 존재하지 않습니다: {path}");
        return File.ReadAllText(path);
    }

    private static string ReadCopierSource()
    {
        Assert.IsTrue(
            AITPackagePathResolver.TryResolveFile(
                "Editor/Package/WebGLBuildCopier.cs",
                out string path,
                typeof(AITConvertCore)),
            "WebGLBuildCopier.cs 소스 경로를 찾지 못했습니다.");
        Assert.IsTrue(File.Exists(path), $"파일이 존재하지 않습니다: {path}");
        return File.ReadAllText(path);
    }
}
