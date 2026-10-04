// -----------------------------------------------------------------------
// AITAutoOptimizerTests.cs - 에디터 로드 시 자동 최적화 판단 함수 검증
// Level 0: 필드 값·배치 모드·설정 에셋 유무·검사 결과·세션 중복 실행에 따른 적용 여부를 확인한다.
// -----------------------------------------------------------------------

using NUnit.Framework;
using AppsInToss.Editor;

[TestFixture]
public class AITAutoOptimizerTests
{
    [Test]
    public void ShouldAutoApply_Auto_And_Explicit_On_Apply_When_Recommended()
    {
        Assert.IsTrue(AITAutoOptimizer.ShouldAutoApply(-1, false, true, true, false));
        Assert.IsTrue(AITAutoOptimizer.ShouldAutoApply(1, false, true, true, false));
    }

    [Test]
    public void ShouldAutoApply_OptOut_Zero_Never_Applies()
    {
        Assert.IsFalse(AITAutoOptimizer.ShouldAutoApply(0, false, true, true, false));
    }

    [Test]
    public void ShouldAutoApply_BatchMode_Never_Applies()
    {
        Assert.IsFalse(AITAutoOptimizer.ShouldAutoApply(-1, true, true, true, false));
    }

    [Test]
    public void ShouldAutoApply_Skips_Without_Config_Asset()
    {
        Assert.IsFalse(AITAutoOptimizer.ShouldAutoApply(-1, false, false, true, false));
    }

    [Test]
    public void ShouldAutoApply_Skips_When_Not_Recommended()
    {
        Assert.IsFalse(AITAutoOptimizer.ShouldAutoApply(-1, false, true, false, false));
    }

    [Test]
    public void ShouldAutoApply_Skips_When_Already_Ran_This_Session()
    {
        Assert.IsFalse(AITAutoOptimizer.ShouldAutoApply(-1, false, true, true, true));
    }

    [Test]
    public void Decide_AutoAppliedBefore_And_LeverEnabled_Returns_OptOut()
    {
        Assert.AreEqual(AITAutoOptimizer.Decision.OptOut,
            AITAutoOptimizer.Decide(-1, false, true, true, false, true, true));
    }

    [Test]
    public void Decide_AutoAppliedBefore_And_LeverStillOff_Skips()
    {
        Assert.AreEqual(AITAutoOptimizer.Decision.Skip,
            AITAutoOptimizer.Decide(-1, false, true, false, false, true, false));
    }

    [Test]
    public void Decide_OptOut_Not_Returned_When_Field_Zero_Or_Batch()
    {
        Assert.AreEqual(AITAutoOptimizer.Decision.Skip, AITAutoOptimizer.Decide(0, false, true, true, false, true, true));
        Assert.AreEqual(AITAutoOptimizer.Decision.Skip, AITAutoOptimizer.Decide(-1, true, true, true, false, true, true));
    }

    [Test]
    public void Decide_First_Time_Recommended_Applies()
    {
        Assert.AreEqual(AITAutoOptimizer.Decision.Apply,
            AITAutoOptimizer.Decide(-1, false, true, true, false, false, true));
    }

    [Test]
    public void NewConfig_Defaults_To_Auto()
    {
        var config = UnityEngine.ScriptableObject.CreateInstance<AppsInToss.AITEditorScriptObject>();
        try
        {
            Assert.AreEqual(-1, config.physicsBackendAutoDisable);
            Assert.AreEqual(-1, config.uiToolkitAutoRemove);
            Assert.AreEqual(0, config.physicsBackendAutoApplied);
            Assert.AreEqual(0, config.uiToolkitAutoApplied);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(config);
        }
    }
}
