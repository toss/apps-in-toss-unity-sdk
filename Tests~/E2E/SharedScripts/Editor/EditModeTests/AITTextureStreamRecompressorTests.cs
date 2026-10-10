// -----------------------------------------------------------------------
// AITTextureStreamRecompressorTests.cs - 스트림 PNG 무손실 재압축 판정 규칙 검증
// Level 0: IsEnabled / ShouldAdopt (순수 함수)
//
// 핵심 불변식:
//   1) 무손실 레버라 auto(-1) 기본 활성 — lossy 레버(audioStreamTranscode, 기본 OFF)와
//      의도적으로 다른 posture. 기본값을 바꾸면 이 테스트와 문서를 함께 갱신할 것.
//   2) 채택 게이트: 1바이트라도 작으면 교체(품질 트레이드오프가 없어 최소 이득 게이트 불필요),
//      같거나 크면 원본 유지.
// -----------------------------------------------------------------------

using NUnit.Framework;
using UnityEngine;
using AppsInToss;
using AppsInToss.Editor;

[TestFixture]
public class AITTextureStreamRecompressorTests
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
        }
    }

    // ─────────────────────────── IsEnabled (tri-state) ───────────────────────────

    [Test]
    public void IsEnabled_NullConfig_ReturnsFalse()
    {
        Assert.IsFalse(AITTextureStreamRecompressor.IsEnabled(null));
    }

    [Test]
    public void IsEnabled_Auto_FollowsSdkDefaultOn()
    {
        _config.textureStreamRecompress = -1;
        Assert.IsTrue(AITDefaultSettings.GetDefaultTextureStreamRecompress(),
            "무손실 레버는 auto 기본 ON 이어야 한다 — 끄려면 이 테스트와 문서를 함께 갱신할 것");
        Assert.IsTrue(AITTextureStreamRecompressor.IsEnabled(_config));
    }

    [Test]
    public void IsEnabled_ExplicitOff_ReturnsFalse()
    {
        _config.textureStreamRecompress = 0;
        Assert.IsFalse(AITTextureStreamRecompressor.IsEnabled(_config));
    }

    [Test]
    public void IsEnabled_ExplicitOn_ReturnsTrue()
    {
        _config.textureStreamRecompress = 1;
        Assert.IsTrue(AITTextureStreamRecompressor.IsEnabled(_config));
    }

    // ─────────────────────────── ShouldAdopt ───────────────────────────

    [Test]
    public void ShouldAdopt_AnySmaller_ReturnsTrue()
    {
        Assert.IsTrue(AITTextureStreamRecompressor.ShouldAdopt(100L, 99L), "무손실이라 1바이트 절감도 채택");
        Assert.IsTrue(AITTextureStreamRecompressor.ShouldAdopt(4_258_827L, 2_915_593L));
    }

    [Test]
    public void ShouldAdopt_EqualOrLarger_ReturnsFalse()
    {
        Assert.IsFalse(AITTextureStreamRecompressor.ShouldAdopt(100L, 100L), "동일 크기는 교체 불필요");
        Assert.IsFalse(AITTextureStreamRecompressor.ShouldAdopt(100L, 120L));
    }

    [Test]
    public void ShouldAdopt_InvalidSizes_ReturnsFalse()
    {
        Assert.IsFalse(AITTextureStreamRecompressor.ShouldAdopt(0L, 0L));
        Assert.IsFalse(AITTextureStreamRecompressor.ShouldAdopt(100L, 0L), "빈 산출물은 원본 유지");
        Assert.IsFalse(AITTextureStreamRecompressor.ShouldAdopt(-1L, 10L));
    }
}
