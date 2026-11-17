// -----------------------------------------------------------------------
// AITPlayerPrefsPersistenceConfigTests.cs - PlayerPrefs 영속화 설정 순수 로직 검증
// Level 0: AssetDatabase 비의존 순수 헬퍼 함수 EditMode 테스트
//   - EffectivePlayerPrefsPersistence: null/tri-state 해석 (fail-open 포함)
// -----------------------------------------------------------------------

using NUnit.Framework;
using UnityEngine;
using AppsInToss;
using AppsInToss.Editor;
using AppsInToss.Editor.Package;

[TestFixture]
public class AITPlayerPrefsPersistenceConfigTests
{
    private AITEditorScriptObject _config;

    [SetUp]
    public void SetUp()
    {
        _config = ScriptableObject.CreateInstance<AITEditorScriptObject>();
    }

    [TearDown]
    public void TearDown()
    {
        if (_config != null)
        {
            Object.DestroyImmediate(_config);
            _config = null;
        }
    }

    // =====================================================
    // 1) EffectivePlayerPrefsPersistence — null → true (fail-open)
    // =====================================================

    [Test]
    public void EffectivePlayerPrefsPersistence_NullConfig_ReturnsTrue_FailOpen()
    {
        // 설정 로드 실패 시에도 PlayerPrefs 보호를 침묵시키면 안 됨
        Assert.IsTrue(WebGLBuildCopier.EffectivePlayerPrefsPersistence(null),
            "null config 시 fail-open이어야 합니다(PlayerPrefs 보호 침묵 방지).");
    }

    // =====================================================
    // 2) EffectivePlayerPrefsPersistence — tri-state 해석
    //    -1 기본값이 GetDefaultPlayerPrefsPersistence()와 일치하는지 대조
    // =====================================================

    [TestCase(-1, true, "-1(자동)은 GetDefaultPlayerPrefsPersistence()==true 와 일치해야 합니다.")]
    [TestCase(0, false, "명시적 비활성(0)은 false 이어야 합니다.")]
    [TestCase(1, true, "명시적 활성(1)은 true 이어야 합니다.")]
    public void EffectivePlayerPrefsPersistence_TriState_ReturnsExpected(int storedValue, bool expected, string reason)
    {
        _config.playerPrefsPersistence = storedValue;
        Assert.AreEqual(expected, WebGLBuildCopier.EffectivePlayerPrefsPersistence(_config), reason);
    }
}
