// -----------------------------------------------------------------------
// TemplateIntegrityTests.cs
// Level 0: WebGLTemplates/AITTemplate 회귀 가드 — 삭제된 자체 Mock 브리지
// (appsintoss-unity-bridge.js)가 재유입되지 않는지 검증한다.
// devtools(@apps-in-toss/devtools) 도입으로 자체 Mock 브리지는 전면 삭제되었고,
// index.html도 관련 script 태그/초기화 코드가 없어야 한다.
// -----------------------------------------------------------------------

using System.IO;
using NUnit.Framework;
using AppsInToss;
using AppsInToss.Editor;

[TestFixture]
public class TemplateIntegrityTests
{
    private const string BridgeReferenceSubstring = "appsintoss-unity-bridge";

    [Test]
    public void IndexHtml_DoesNotReferenceRemovedBridgeScript()
    {
        string templateRoot = ResolveTemplateRoot();
        string indexHtmlPath = Path.Combine(templateRoot, "index.html");
        Assert.IsTrue(File.Exists(indexHtmlPath), $"파일이 존재하지 않습니다: {indexHtmlPath}");

        string content = File.ReadAllText(indexHtmlPath);

        StringAssert.DoesNotContain(
            BridgeReferenceSubstring,
            content,
            "index.html에 삭제된 자체 Mock 브리지(appsintoss-unity-bridge) 참조가 재유입되었습니다.");
    }

    [Test]
    public void RuntimeBridgeScript_FileDoesNotExist()
    {
        string templateRoot = ResolveTemplateRoot();
        string bridgeJsPath = Path.Combine(templateRoot, "Runtime", "appsintoss-unity-bridge.js");

        Assert.IsFalse(
            File.Exists(bridgeJsPath),
            $"삭제되었어야 할 자체 Mock 브리지 파일이 재유입되었습니다: {bridgeJsPath}");
    }

    private static string ResolveTemplateRoot()
    {
        Assert.IsTrue(
            AITPackagePathResolver.TryResolveDirectory(
                "WebGLTemplates", out string webglTemplatesPath, typeof(AITConvertCore)),
            "SDK WebGLTemplates 폴더를 찾지 못했습니다.");

        string templateRoot = Path.Combine(webglTemplatesPath, "AITTemplate");
        Assert.IsTrue(Directory.Exists(templateRoot), $"디렉토리가 존재하지 않습니다: {templateRoot}");
        return templateRoot;
    }
}
