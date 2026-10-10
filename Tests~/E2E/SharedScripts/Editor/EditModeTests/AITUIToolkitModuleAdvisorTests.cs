// -----------------------------------------------------------------------
// AITUIToolkitModuleAdvisorTests.cs - UI Toolkit 모듈 안내 검사 검증
// Level 0: 실제 프로젝트에서 Analyze() 가 예외 없이 끝나고 manifest 상태를 그대로 읽는지 확인한다.
// -----------------------------------------------------------------------

using System.IO;
using NUnit.Framework;
using UnityEngine;
using AppsInToss.Editor;

[TestFixture]
public class AITUIToolkitModuleAdvisorTests
{
    [Test]
    public void Analyze_Completes_And_Reads_Manifest()
    {
        var result = AITUIToolkitModuleAdvisor.Analyze();

        Assert.IsFalse(result.Failed, "검사가 예외로 끝나면 안 된다");

        string manifest = File.ReadAllText(Path.Combine(Path.GetDirectoryName(Application.dataPath), "Packages", "manifest.json"));
        Assert.AreEqual(manifest.Contains("\"" + AITUIToolkitModuleAdvisor.ModuleName + "\""), result.ModuleEnabled);

        if (!result.ModuleEnabled)
        {
            Assert.IsFalse(result.Recommended, "모듈이 꺼져 있으면 끄기를 권하지 않는다");
        }
    }
}
