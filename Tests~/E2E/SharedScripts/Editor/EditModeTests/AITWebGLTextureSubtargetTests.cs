// -----------------------------------------------------------------------
// AITWebGLTextureSubtargetTests.cs - 자동 ASTC 서브타겟 결정 로직 EditMode 테스트
// -----------------------------------------------------------------------

using NUnit.Framework;
using AppsInToss.Editor;

[TestFixture]
public class AITWebGLTextureSubtargetTests
{
    [TestCase(-1, "Generic", true)]
    [TestCase(-1, "DXT", true)]
    [TestCase(-1, "ETC2", false)]
    [TestCase(-1, "ASTC", false)]
    [TestCase(0, "Generic", false)]
    [TestCase(0, "DXT", false)]
    public void ShouldSwitchToAstc_Decision(int setting, string current, bool expected)
    {
        Assert.AreEqual(expected, AITWebGLTextureSubtarget.ShouldSwitchToAstc(setting, current));
    }

    [Test]
    public void ApplyForBuild_NullConfig_ReturnsInactiveHandle()
    {
        var handle = AITWebGLTextureSubtarget.ApplyForBuild(null);
        Assert.IsNotNull(handle);
        Assert.IsFalse(handle.Active);
        AITWebGLTextureSubtarget.RestoreForBuild(handle);
        AITWebGLTextureSubtarget.RestoreForBuild(null);
    }

    [Test]
    public void DefaultConfig_IsAuto()
    {
        var config = UnityEngine.ScriptableObject.CreateInstance<AITEditorScriptObject>();
        try { Assert.AreEqual(-1, config.webglTextureSubtargetAuto); }
        finally { UnityEngine.Object.DestroyImmediate(config); }
    }
}
