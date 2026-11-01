// -----------------------------------------------------------------------
// AITPhysicsBackendAdvisorTests.cs - 물리 백엔드(PhysX) 안내 검사 검증
// Level 0: Analyze() 가 예외 없이 끝나고 에셋 파일 상태를 그대로 읽는지, 에셋 텍스트 재작성이 맞는지 확인한다.
// -----------------------------------------------------------------------

using System.IO;
using NUnit.Framework;
using UnityEngine;
using AppsInToss.Editor;

[TestFixture]
public class AITPhysicsBackendAdvisorTests
{
    [Test]
    public void Analyze_Completes_And_Reads_AssetFile()
    {
        var result = AITPhysicsBackendAdvisor.Analyze();

        Assert.IsFalse(result.Failed, "검사가 예외로 끝나면 안 된다");

#if UNITY_6000_3_OR_NEWER
        Assert.IsTrue(result.Applicable);
        string path = Path.Combine(Path.GetDirectoryName(Application.dataPath), "ProjectSettings", "DynamicsManager.asset");
        string text = File.Exists(path) ? File.ReadAllText(path) : "";
        Assert.AreEqual(AITPhysicsBackendAdvisor.IsPhysXEnabled(text), result.PhysXEnabled);
        if (!result.PhysXEnabled)
        {
            Assert.IsFalse(result.Recommended, "PhysX가 꺼져 있으면 끄기를 권하지 않는다");
        }
#else
        Assert.IsFalse(result.Applicable, "6000.3 미만에서는 적용 대상이 아니다");
        Assert.IsFalse(result.Recommended);
#endif
    }

    [Test]
    public void RewriteBackendId_Replaces_Existing_Line()
    {
        string text = "%YAML 1.1\nPhysicsManager:\n  m_DefaultMaxAngularSpeed: 7\n  m_CurrentBackendId: 4072204805\n  m_Other: 1\n";

        string result = AITPhysicsBackendAdvisor.RewriteBackendId(text, AITPhysicsBackendAdvisor.NoneBackendId);

        Assert.AreEqual("%YAML 1.1\nPhysicsManager:\n  m_DefaultMaxAngularSpeed: 7\n  m_CurrentBackendId: 3737844653\n  m_Other: 1\n", result);
        Assert.IsFalse(AITPhysicsBackendAdvisor.IsPhysXEnabled(result));
    }

    [Test]
    public void RewriteBackendId_Inserts_When_Absent()
    {
        string text = "%YAML 1.1\nPhysicsManager:\n  m_DefaultMaxAngularSpeed: 7";

        string result = AITPhysicsBackendAdvisor.RewriteBackendId(text, AITPhysicsBackendAdvisor.NoneBackendId);

        Assert.AreEqual("%YAML 1.1\nPhysicsManager:\n  m_DefaultMaxAngularSpeed: 7\n  m_CurrentBackendId: 3737844653\n", result);
        Assert.AreEqual(AITPhysicsBackendAdvisor.NoneBackendId, AITPhysicsBackendAdvisor.ReadBackendId(result));
        Assert.IsTrue(AITPhysicsBackendAdvisor.IsPhysXEnabled(text), "키가 없으면 기본값(PhysX)이다");
    }
}
