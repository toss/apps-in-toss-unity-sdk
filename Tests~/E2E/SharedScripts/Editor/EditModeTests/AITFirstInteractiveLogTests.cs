// -----------------------------------------------------------------------
// AITFirstInteractiveLogTests.cs - first-interactive 계측 순수 로직 검증
// Level 0: AssetDatabase 비의존 순수 헬퍼 함수 EditMode 테스트
//   - EffectiveFirstInteractiveLog: null/tri-state 해석 (fail-open 포함)
//   - ShouldEmitFirstInteractive: alreadySent, 프록시 씬 접두, null/빈 씬명, Ordinal 대소문자
// -----------------------------------------------------------------------

using NUnit.Framework;
using UnityEngine;
using AppsInToss;
using AppsInToss.Editor;
using AppsInToss.Editor.Package;

[TestFixture]
public class AITFirstInteractiveLogTests
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
    // 1) EffectiveFirstInteractiveLog — null → true (fail-open)
    // =====================================================

    [Test]
    public void EffectiveFirstInteractiveLog_NullConfig_ReturnsTrue_FailOpen()
    {
        // 계측기는 픽셀 불변이므로 설정 로드 실패 시에도 계측을 침묵시키면 안 됨
        Assert.IsTrue(WebGLBuildCopier.EffectiveFirstInteractiveLog(null),
            "null config 시 fail-open이어야 합니다(계측 침묵 방지).");
    }

    // =====================================================
    // 2) EffectiveFirstInteractiveLog — tri-state 해석
    //    -1 기본값이 GetDefaultFirstInteractiveLog()와 일치하는지 대조
    // =====================================================

    [TestCase(-1, true, "-1(자동)은 GetDefaultFirstInteractiveLog()==true 와 일치해야 합니다.")]
    [TestCase(0, false, "명시적 비활성(0)은 false 이어야 합니다.")]
    [TestCase(1, true, "명시적 활성(1)은 true 이어야 합니다.")]
    public void EffectiveFirstInteractiveLog_TriState_ReturnsExpected(int storedValue, bool expected, string reason)
    {
        _config.firstInteractiveLog = storedValue;
        Assert.AreEqual(expected, WebGLBuildCopier.EffectiveFirstInteractiveLog(_config), reason);
    }

    // =====================================================
    // 3) ShouldEmitFirstInteractive — alreadySent=true 는 항상 false
    // =====================================================

    [Test]
    public void ShouldEmitFirstInteractive_AlreadySent_ReturnsFalse()
    {
        Assert.IsFalse(AITPerformanceLogger.ShouldEmitFirstInteractive("Title", alreadySent: true),
            "alreadySent=true 이면 씬명 무관하게 false 이어야 합니다.");
    }

    // =====================================================
    // 4) ShouldEmitFirstInteractive — 정상 씬명 + alreadySent=false → true
    // =====================================================

    [Test]
    public void ShouldEmitFirstInteractive_NormalScene_NotSent_ReturnsTrue()
    {
        Assert.IsTrue(AITPerformanceLogger.ShouldEmitFirstInteractive("Title", alreadySent: false),
            "일반 씬명 + alreadySent=false 이면 true 이어야 합니다.");
    }

    // =====================================================
    // 5) ShouldEmitFirstInteractive — "AITProxyBoot" 접두 씬 → false
    //    (SDK 주입 프록시 부팅 씬은 원래 첫 씬이 아님)
    // =====================================================

    [Test]
    public void ShouldEmitFirstInteractive_ProxyBootExact_ReturnsFalse()
    {
        Assert.IsFalse(AITPerformanceLogger.ShouldEmitFirstInteractive("AITProxyBoot", alreadySent: false),
            "'AITProxyBoot' 씬은 false 이어야 합니다.");
    }

    [Test]
    public void ShouldEmitFirstInteractive_ProxyBootPrefixScene_ReturnsFalse()
    {
        Assert.IsFalse(AITPerformanceLogger.ShouldEmitFirstInteractive("AITProxyBootScene01", alreadySent: false),
            "'AITProxyBoot'로 시작하는 씬은 접두 일치로 false 이어야 합니다.");
    }

    // =====================================================
    // 6) ShouldEmitFirstInteractive — null/빈 씬명 → true
    //    (이름 없는 씬도 최초 실 씬으로 취급; StartsWith NRE 방어)
    // =====================================================

    [Test]
    public void ShouldEmitFirstInteractive_NullSceneName_ReturnsTrue()
    {
        Assert.IsTrue(AITPerformanceLogger.ShouldEmitFirstInteractive(null, alreadySent: false),
            "null 씬명은 true 이어야 합니다(StartsWith NRE 방어).");
    }

    [Test]
    public void ShouldEmitFirstInteractive_EmptySceneName_ReturnsTrue()
    {
        Assert.IsTrue(AITPerformanceLogger.ShouldEmitFirstInteractive("", alreadySent: false),
            "빈 씬명은 true 이어야 합니다.");
    }

    // =====================================================
    // 7) ShouldEmitFirstInteractive — Ordinal 대소문자: 소문자 'aitproxyboot' → true
    //    (StringComparison.Ordinal이므로 소문자 접두는 프록시 씬으로 취급하지 않음)
    // =====================================================

    [Test]
    public void ShouldEmitFirstInteractive_LowercaseProxyBoot_ReturnsTrueOrdinalOnly()
    {
        Assert.IsTrue(AITPerformanceLogger.ShouldEmitFirstInteractive("aitproxyboot", alreadySent: false),
            "소문자 'aitproxyboot'는 Ordinal 비교이므로 true 이어야 합니다(대소문자 구분).");
    }
}
