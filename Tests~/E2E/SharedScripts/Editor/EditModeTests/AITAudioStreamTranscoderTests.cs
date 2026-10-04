// -----------------------------------------------------------------------
// AITAudioStreamTranscoderTests.cs - 스트림 오디오 재인코딩 판정 규칙 검증
// Level 0: IsEnabled / EstimateKbps / ShouldTranscode / ShouldAdopt / Resolve* (순수 함수)
//
// 핵심 불변식:
//   1) auto(-1)는 기본 ON, 0 으로 끈다. 루프 클립(AudioSource.loop)은 auto 에서 제외(ScanYamlForLoopingClips).
//   2) ShouldTranscode 하한 방어: minSourceKbps 가 target 이하로 잘못 설정돼도
//      target+32 미만 소스는 재인코딩하지 않는다 (세대손실만 남는 재인코딩 차단).
//   3) 채택 게이트: 산출물이 원본 대비 25% 이상 작을 때만 교체 (미달 시 원본 유지).
// -----------------------------------------------------------------------

using NUnit.Framework;
using UnityEngine;
using AppsInToss;
using AppsInToss.Editor;

[TestFixture]
public class AITAudioStreamTranscoderTests
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
        Assert.IsFalse(AITAudioStreamTranscoder.IsEnabled(null));
    }

    [Test]
    public void IsEnabled_Auto_FollowsSdkDefaultOn()
    {
        _config.audioStreamTranscode = -1;
        Assert.IsTrue(AITDefaultSettings.GetDefaultAudioStreamTranscode(),
            "auto 기본은 ON 이어야 한다(루프 클립은 별도 게이트로 제외) — 끄려면 audioStreamTranscode=0");
        Assert.IsTrue(AITAudioStreamTranscoder.IsEnabled(_config));
    }

    private const string LoopScene =
        "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n" +
        "--- !u!82 &100\nAudioSource:\n  m_ObjectHideFlags: 0\n  m_audioClip: {fileID: 8300000, guid: aaaa1111, type: 3}\n  Loop: 1\n  Mute: 0\n" +
        "--- !u!82 &200\nAudioSource:\n  m_audioClip: {fileID: 8300000, guid: bbbb2222, type: 3}\n  Loop: 0\n" +
        "--- !u!114 &300\nMonoBehaviour:\n  m_audioClip: {fileID: 8300000, guid: cccc3333, type: 3}\n  Loop: 1\n";

    [Test]
    public void ScanYamlForLoopingClips_OnlyLoopingAudioSourceClips()
    {
        var sink = new System.Collections.Generic.HashSet<string>();
        AITAudioStreamTranscoder.ScanYamlForLoopingClips(LoopScene, sink);
        CollectionAssert.AreEquivalent(new[] { "aaaa1111" }, sink,
            "Loop: 1 인 AudioSource 의 클립만 포함해야 한다(Loop: 0 AudioSource, 다른 컴포넌트는 제외).");
    }

    [Test]
    public void ScanYamlForLoopingClips_PrefabLoopOverride_IncludesOverriddenClips()
    {
        string yaml = "%YAML 1.1\n--- !u!1001 &1\nPrefabInstance:\n  m_Modification:\n    m_Modifications:\n" +
            "    - target: {fileID: 1, guid: p, type: 3}\n      propertyPath: Loop\n      value: 1\n      objectReference: {fileID: 0}\n" +
            "    - target: {fileID: 1, guid: p, type: 3}\n      propertyPath: m_audioClip\n      value: \n      objectReference: {fileID: 8300000, guid: dddd4444, type: 3}\n";
        var sink = new System.Collections.Generic.HashSet<string>();
        AITAudioStreamTranscoder.ScanYamlForLoopingClips(yaml, sink);
        CollectionAssert.AreEquivalent(new[] { "dddd4444" }, sink);
    }

    [Test]
    public void ScanYamlForLoopingClips_NullOrNoAudioSource_IsNoOp()
    {
        var sink = new System.Collections.Generic.HashSet<string>();
        AITAudioStreamTranscoder.ScanYamlForLoopingClips(null, sink);
        AITAudioStreamTranscoder.ScanYamlForLoopingClips("%YAML 1.1\n--- !u!1 &1\nGameObject:\n", sink);
        Assert.AreEqual(0, sink.Count);
    }

    [Test]
    public void IsEnabled_ExplicitOn_ReturnsTrue()
    {
        _config.audioStreamTranscode = 1;
        Assert.IsTrue(AITAudioStreamTranscoder.IsEnabled(_config));
    }

    [Test]
    public void IsEnabled_ExplicitOff_ReturnsFalse()
    {
        _config.audioStreamTranscode = 0;
        Assert.IsFalse(AITAudioStreamTranscoder.IsEnabled(_config));
    }

    // ─────────────────────────── EstimateKbps ───────────────────────────

    [Test]
    public void EstimateKbps_KnownRatio_ComputesAverageBitrate()
    {
        // 1,000,000 bytes / 50s = 8,000,000 bits / 50 / 1000 = 160 kbps
        Assert.AreEqual(160, AITAudioStreamTranscoder.EstimateKbps(1_000_000L, 50f));
    }

    [Test]
    public void EstimateKbps_TooShortOrEmpty_ReturnsZero()
    {
        Assert.AreEqual(0, AITAudioStreamTranscoder.EstimateKbps(0L, 30f));
        Assert.AreEqual(0, AITAudioStreamTranscoder.EstimateKbps(-1L, 30f));
        Assert.AreEqual(0, AITAudioStreamTranscoder.EstimateKbps(1_000_000L, 0.4f));
    }

    // ─────────────────────────── ShouldTranscode ───────────────────────────

    [Test]
    public void ShouldTranscode_HighBitrateSource_ReturnsTrue()
    {
        // 320kbps 소스 (2,400,000 bytes / 60s), 게이트 256, 목표 160 → 대상
        Assert.IsTrue(AITAudioStreamTranscoder.ShouldTranscode(2_400_000L, 60f, 256, 160));
    }

    [Test]
    public void ShouldTranscode_SourceBelowGate_ReturnsFalse()
    {
        // 192kbps 소스 (1,440,000 bytes / 60s), 게이트 256 → 제외
        Assert.IsFalse(AITAudioStreamTranscoder.ShouldTranscode(1_440_000L, 60f, 256, 160));
    }

    [Test]
    public void ShouldTranscode_MisconfiguredLowGate_FloorDefenseHolds()
    {
        // 게이트를 96 으로 잘못 낮춰도 target+32=192 미만 소스(160kbps)는 제외돼야 한다.
        Assert.IsFalse(AITAudioStreamTranscoder.ShouldTranscode(1_200_000L, 60f, 96, 160));
        // 정확히 floor(192kbps) 이상이면 대상.
        Assert.IsTrue(AITAudioStreamTranscoder.ShouldTranscode(1_440_000L, 60f, 96, 160));
    }

    [Test]
    public void ShouldTranscode_UnmeasurableDuration_ReturnsFalse()
    {
        Assert.IsFalse(AITAudioStreamTranscoder.ShouldTranscode(2_400_000L, 0.1f, 256, 160));
    }

    // ─────────────────────────── ShouldAdopt (25% 게이트) ───────────────────────────

    [Test]
    public void ShouldAdopt_ExactGateBoundary()
    {
        // 원본 대비 25% 이상 축소만 채택: 100 → 75 채택, 100 → 76 원본 유지.
        Assert.IsTrue(AITAudioStreamTranscoder.ShouldAdopt(100L, 75L));
        Assert.IsFalse(AITAudioStreamTranscoder.ShouldAdopt(100L, 76L));
    }

    [Test]
    public void ShouldAdopt_LargerOutput_ReturnsFalse()
    {
        Assert.IsFalse(AITAudioStreamTranscoder.ShouldAdopt(100L, 120L));
    }

    // ─────────────────────────── Resolve* (클램프/기본값) ───────────────────────────

    [Test]
    public void ResolveTargetKbps_InvalidOrNull_FallsBackTo160()
    {
        Assert.AreEqual(160, AITAudioStreamTranscoder.ResolveTargetKbps(null));
        _config.audioStreamTranscodeBitrateKbps = 0;
        Assert.AreEqual(160, AITAudioStreamTranscoder.ResolveTargetKbps(_config));
        _config.audioStreamTranscodeBitrateKbps = -8;
        Assert.AreEqual(160, AITAudioStreamTranscoder.ResolveTargetKbps(_config));
    }

    [Test]
    public void ResolveTargetKbps_ClampsToMp3Range()
    {
        _config.audioStreamTranscodeBitrateKbps = 64;
        Assert.AreEqual(96, AITAudioStreamTranscoder.ResolveTargetKbps(_config), "하한 96 클램프");
        _config.audioStreamTranscodeBitrateKbps = 512;
        Assert.AreEqual(320, AITAudioStreamTranscoder.ResolveTargetKbps(_config), "상한 320 클램프");
        _config.audioStreamTranscodeBitrateKbps = 192;
        Assert.AreEqual(192, AITAudioStreamTranscoder.ResolveTargetKbps(_config), "정상 값은 그대로");
    }

    [Test]
    public void ResolveMinSourceKbps_InvalidOrNull_FallsBackTo256()
    {
        Assert.AreEqual(256, AITAudioStreamTranscoder.ResolveMinSourceKbps(null));
        _config.audioStreamTranscodeMinSourceKbps = 0;
        Assert.AreEqual(256, AITAudioStreamTranscoder.ResolveMinSourceKbps(_config));
        _config.audioStreamTranscodeMinSourceKbps = 300;
        Assert.AreEqual(300, AITAudioStreamTranscoder.ResolveMinSourceKbps(_config));
    }

    [Test]
    public void IsLikelyBgmByLength_ThresholdIs20Seconds()
    {
        Assert.IsFalse(AITAudioStreamTranscoder.IsLikelyBgmByLength(0f), "길이 미상은 제외하지 않음");
        Assert.IsFalse(AITAudioStreamTranscoder.IsLikelyBgmByLength(3.5f), "짧은 효과음은 대상");
        Assert.IsFalse(AITAudioStreamTranscoder.IsLikelyBgmByLength(20f), "경계 20초는 대상");
        Assert.IsTrue(AITAudioStreamTranscoder.IsLikelyBgmByLength(20.1f), "20초 초과는 BGM 가능성 → 자동 제외");
        Assert.IsTrue(AITAudioStreamTranscoder.IsLikelyBgmByLength(180f));
    }
}
