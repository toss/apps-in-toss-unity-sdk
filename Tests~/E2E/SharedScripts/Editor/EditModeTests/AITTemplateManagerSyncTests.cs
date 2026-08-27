// -----------------------------------------------------------------------
// AITTemplateManagerSyncTests.cs - 프로젝트 템플릿 동기화 회귀 테스트
// Level 0: index.html이 없을 때 SyncProjectTemplate이 프로젝트 소유 BuildConfig~를
//          지우지 않는지 검증 (E2E 픽스처의 BuildConfig~/src/main.ts 소실 회귀)
// -----------------------------------------------------------------------

using System.IO;
using NUnit.Framework;
using AppsInToss.Editor;

[TestFixture]
public class AITTemplateManagerSyncTests
{
    private string _root;
    private string _sdkTemplates;
    private string _projectTemplates;

    private static string SdkIndexHtml =>
        "<html>\n<head>\n" +
        AITTemplateManager.HTML_USER_HEAD_START + " -->\n" +
        AITTemplateManager.HTML_USER_HEAD_END + "\n" +
        "</head>\n<body>\n" +
        AITTemplateManager.HTML_USER_BODY_END_START + " -->\n" +
        AITTemplateManager.HTML_USER_BODY_END_END + "\n" +
        "</body>\n</html>\n";

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "AITTemplateSyncTests_" + Path.GetRandomFileName());
        _sdkTemplates = Path.Combine(_root, "sdk");
        _projectTemplates = Path.Combine(_root, "project");

        string sdkTemplate = Path.Combine(_sdkTemplates, "AITTemplate");
        Directory.CreateDirectory(sdkTemplate);
        File.WriteAllText(Path.Combine(sdkTemplate, "index.html"), SdkIndexHtml);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    /// <summary>
    /// index.html만 없는 상태에서 동기화해도 프로젝트가 소유한 BuildConfig~ 파일은 남아야 한다.
    /// SDK 정본에는 없는 파일이라 한 번 지우면 복사로 되살아나지 않는다.
    /// </summary>
    [Test]
    public void SyncProjectTemplate_MissingIndexHtml_PreservesProjectBuildConfig()
    {
        string projectTemplate = Path.Combine(_projectTemplates, "AITTemplate");
        string projectMainTs = Path.Combine(projectTemplate, "BuildConfig~", "src", "main.ts");
        Directory.CreateDirectory(Path.GetDirectoryName(projectMainTs));
        File.WriteAllText(projectMainTs, "export const tutorial = true;\n");

        AITTemplateManager.SyncProjectTemplate(_projectTemplates, _sdkTemplates);

        FileAssert.Exists(projectMainTs);
        Assert.AreEqual("export const tutorial = true;\n", File.ReadAllText(projectMainTs));
        FileAssert.Exists(Path.Combine(projectTemplate, "index.html"));
    }

    /// <summary>
    /// 템플릿 폴더 자체가 없으면 잃을 자산이 없으므로 정본 전체 복사가 그대로 동작해야 한다.
    /// </summary>
    [Test]
    public void SyncProjectTemplate_MissingProjectTemplate_CopiesSdkTemplate()
    {
        bool changed = AITTemplateManager.SyncProjectTemplate(_projectTemplates, _sdkTemplates);

        Assert.IsTrue(changed);
        FileAssert.Exists(Path.Combine(_projectTemplates, "AITTemplate", "index.html"));
    }

    /// <summary>
    /// index.html을 복구할 때 사용자 커스텀 영역이 남아 있는 다른 파일까지 건드리면 안 된다.
    /// 복구는 index.html 한 파일에 한정된다.
    /// </summary>
    [Test]
    public void SyncProjectTemplate_MissingIndexHtml_RestoresOnlyIndexHtml()
    {
        string projectTemplate = Path.Combine(_projectTemplates, "AITTemplate");
        string projectOwned = Path.Combine(projectTemplate, "custom-asset.txt");
        Directory.CreateDirectory(projectTemplate);
        File.WriteAllText(projectOwned, "keep me");

        AITTemplateManager.SyncProjectTemplate(_projectTemplates, _sdkTemplates);

        FileAssert.Exists(projectOwned);
        Assert.AreEqual("keep me", File.ReadAllText(projectOwned));
    }
}
