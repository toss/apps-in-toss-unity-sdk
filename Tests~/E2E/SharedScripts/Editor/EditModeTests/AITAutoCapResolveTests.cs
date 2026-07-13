// -----------------------------------------------------------------------
// AITAutoCapResolveTests.cs - auto 모드 캡 해석 규칙 검증
// Level 0: ResolveClampMax / ResolveDownscaleCap (config) → 유효 캡
//
// 핵심 불변식 (stale-serialization 방어):
//   Unity 는 선언 기본값과 같은 값도 에셋에 전부 직렬화하므로, 필드 initializer 를
//   1024→2048 로 바꿔도 "기존" AITConfig.asset 에는 옛 값이 남는다. posture 를
//   auto(opt-out) 로 전환하면 그 stale 값이 활성화되므로:
//   1) auto(-1) / off(0) 모드는 직렬화 캡을 무시하고 SDK 관리 기본값을 쓴다.
//   2) 사용자 튜닝 캡은 명시 활성(==1) 에서만 존중한다.
// -----------------------------------------------------------------------

using NUnit.Framework;
using UnityEngine;
using AppsInToss;
using AppsInToss.Editor;

[TestFixture]
public class AITAutoCapResolveTests
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

    // ─────────────── 클램프 캡 (AITTextureSizeClampProcessor) ───────────────

    [Test]
    public void ResolveClampMax_Auto_IgnoresStaleSerializedCap()
    {
        _config.textureSizeClamp = -1;      // auto
        _config.textureClampMaxSize = 1024; // opt-in 시절 잔존 직렬화 값 시뮬레이션

        Assert.AreEqual(AITDefaultSettings.GetDefaultTextureClampMaxSize(),
            AITTextureSizeClampProcessor.ResolveClampMax(_config),
            "auto 모드는 stale 직렬화 캡(1024)이 아니라 SDK 관리 기본값을 써야 한다.");
    }

    [Test]
    public void ResolveClampMax_Explicit_HonorsUserCap()
    {
        _config.textureSizeClamp = 1;       // 명시 활성
        _config.textureClampMaxSize = 1024;

        Assert.AreEqual(1024, AITTextureSizeClampProcessor.ResolveClampMax(_config),
            "명시 활성(==1)에서만 사용자 튜닝 캡을 존중한다.");
    }

    [Test]
    public void ResolveClampMax_Off_ReturnsSdkDefault()
    {
        _config.textureSizeClamp = 0;       // off (캡 값 자체는 사용되지 않지만 규칙 일관성 확인)
        _config.textureClampMaxSize = 1024;

        Assert.AreEqual(AITDefaultSettings.GetDefaultTextureClampMaxSize(),
            AITTextureSizeClampProcessor.ResolveClampMax(_config));
    }

    // ─────────────── 다운스케일 캡 (AITLargeTextureExternalizer) ───────────────

    [Test]
    public void ResolveDownscaleCap_Auto_IgnoresStaleSerializedCap()
    {
        _config.textureStreamDownscale = -1;
        _config.textureStreamDownscaleMaxSize = 1024;

        Assert.AreEqual(AITDefaultSettings.GetDefaultTextureStreamDownscaleMaxSize(),
            AITLargeTextureExternalizer.ResolveDownscaleCap(_config),
            "auto 모드는 stale 직렬화 캡(1024)이 아니라 SDK 관리 기본값을 써야 한다.");
    }

    [Test]
    public void ResolveDownscaleCap_Explicit_HonorsUserCap()
    {
        _config.textureStreamDownscale = 1;
        _config.textureStreamDownscaleMaxSize = 1536;

        Assert.AreEqual(1536, AITLargeTextureExternalizer.ResolveDownscaleCap(_config),
            "명시 활성(==1)에서만 사용자 튜닝 캡을 존중한다.");
    }

    [Test]
    public void ResolveDownscaleCap_Off_ReturnsSdkDefault()
    {
        _config.textureStreamDownscale = 0;
        _config.textureStreamDownscaleMaxSize = 1024;

        Assert.AreEqual(AITDefaultSettings.GetDefaultTextureStreamDownscaleMaxSize(),
            AITLargeTextureExternalizer.ResolveDownscaleCap(_config));
    }

    // ─────────────── 두 캡의 SDK 기본값 정합성 ───────────────

    [Test]
    public void SdkDefaults_ClampAndDownscale_AreConsistent()
    {
        Assert.AreEqual(AITDefaultSettings.GetDefaultTextureClampMaxSize(),
            AITDefaultSettings.GetDefaultTextureStreamDownscaleMaxSize(),
            "클램프/다운스케일 캡은 같은 HiDPI 기준(2048)을 공유한다 — 갈라지면 의도 확인 필요.");
    }
}
