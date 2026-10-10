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

    // ─────────────────────────── P0-6: PCM WAV → AAC-LC (.m4a) ───────────────────────────

    /// <summary>fmt 청크만 있는 최소 RIFF/WAVE 헤더(테스트용). tag 0xFFFE 면 EXTENSIBLE 서브포맷을 subTag 로 채운다.</summary>
    private static byte[] WavHeader(int tag, int channels, int rate, int subTag = 1, bool extensible = false)
    {
        int fmtSize = extensible ? 40 : 16;
        var b = new System.Collections.Generic.List<byte>();
        void Tag(string t) { foreach (char c in t) b.Add((byte)c); }
        void U16(int v) { b.Add((byte)(v & 0xFF)); b.Add((byte)((v >> 8) & 0xFF)); }
        void U32(int v) { U16(v & 0xFFFF); U16((v >> 16) & 0xFFFF); }
        Tag("RIFF"); U32(4 + 8 + fmtSize + 8); Tag("WAVE");
        Tag("fmt "); U32(fmtSize);
        U16(extensible ? 0xFFFE : tag); U16(channels); U32(rate); U32(rate * channels * 2); U16(channels * 2); U16(16);
        if (extensible)
        {
            U16(22); U16(16); U32(3);
            U16(subTag); // SubFormat GUID 앞 2바이트
            for (int i = 0; i < 14; i++) { b.Add(0); }
        }
        Tag("data"); U32(0);
        return b.ToArray();
    }

    [Test]
    public void TryParseWavHeader_PcmStereo_ReadsFormat()
    {
        Assert.IsTrue(AITAudioAacEncoder.TryParseWavHeader(WavHeader(1, 2, 44100), out var info));
        Assert.IsTrue(info.IsPcm);
        Assert.AreEqual(2, info.Channels);
        Assert.AreEqual(44100, info.SampleRate);
    }

    [Test]
    public void TryParseWavHeader_FloatAndExtensible_AreTreatedAsPcm()
    {
        Assert.IsTrue(AITAudioAacEncoder.TryParseWavHeader(WavHeader(3, 1, 48000), out var f));
        Assert.IsTrue(f.IsPcm, "IEEE float(3)도 PCM");
        Assert.IsTrue(AITAudioAacEncoder.TryParseWavHeader(WavHeader(0, 2, 48000, subTag: 1, extensible: true), out var x));
        Assert.AreEqual(1, x.FormatTag, "EXTENSIBLE 은 서브포맷 태그로 풀어 담는다");
        Assert.IsTrue(x.IsPcm);
    }

    [Test]
    public void TryParseWavHeader_Adpcm_IsNotPcm()
    {
        Assert.IsTrue(AITAudioAacEncoder.TryParseWavHeader(WavHeader(2, 2, 22050), out var info));
        Assert.IsFalse(info.IsPcm, "MS-ADPCM 은 이미 압축돼 있어 AAC 로 다시 누르지 않는다");
    }

    [Test]
    public void TryParseWavHeader_GarbageOrTruncated_ReturnsFalseWithoutThrowing()
    {
        Assert.IsFalse(AITAudioAacEncoder.TryParseWavHeader(null, out _));
        Assert.IsFalse(AITAudioAacEncoder.TryParseWavHeader(new byte[10], out _));
        Assert.IsFalse(AITAudioAacEncoder.TryParseWavHeader(System.Text.Encoding.ASCII.GetBytes("ID3\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0"), out _));
        byte[] full = WavHeader(1, 2, 44100);
        byte[] cut = new byte[20];
        System.Array.Copy(full, cut, 20);
        Assert.IsFalse(AITAudioAacEncoder.TryParseWavHeader(cut, out _), "fmt 청크가 잘린 헤더");
    }

    [Test]
    public void AacBitrateKbps_StereoKeepsRequest_MonoIsReduced()
    {
        Assert.AreEqual(160, AITAudioAacEncoder.AacBitrateKbps(160, 2));
        Assert.AreEqual(160, AITAudioAacEncoder.AacBitrateKbps(0, 2), "비정상 요청값은 160");
        Assert.AreEqual(100, AITAudioAacEncoder.AacBitrateKbps(160, 1), "모노 160 -> 5/8");
        Assert.AreEqual(64, AITAudioAacEncoder.AacBitrateKbps(96, 1), "모노 하한 64");
    }

    [Test]
    public void LooksLikeMp4_RequiresFtypBox()
    {
        var mp4 = new byte[] { 0, 0, 0, 0x20, (byte)'f', (byte)'t', (byte)'y', (byte)'p', (byte)'M', (byte)'4', (byte)'A', (byte)' ' };
        Assert.IsTrue(AITAudioAacEncoder.LooksLikeMp4(mp4));
        Assert.IsFalse(AITAudioAacEncoder.LooksLikeMp4(System.Text.Encoding.ASCII.GetBytes("Error: no such file")));
        Assert.IsFalse(AITAudioAacEncoder.LooksLikeMp4(new byte[4]));
        Assert.IsFalse(AITAudioAacEncoder.LooksLikeMp4(null));
    }

    [Test]
    public void BuildFfmpegArgs_FixesContainerCodecAndBitrate()
    {
        var a = AITAudioAacEncoder.BuildFfmpegArgs("/in/a.wav", "/out/a.m4a.tmp", 160);
        CollectionAssert.Contains(a, "aac");
        CollectionAssert.Contains(a, "160k");
        int f = a.IndexOf("-f");
        Assert.GreaterOrEqual(f, 0);
        Assert.AreEqual("mp4", a[f + 1], "임시 확장자에서도 컨테이너가 고정돼야 한다");
        Assert.AreEqual("/out/a.m4a.tmp", a[a.Count - 1], "출력 경로가 마지막 인자");
        Assert.AreEqual("/in/a.wav", a[a.IndexOf("-i") + 1]);
        CollectionAssert.Contains(a, "-map_metadata");
    }

    [Test]
    public void BuildAfconvertArgs_UsesM4afAacInBitsPerSecond()
    {
        var a = AITAudioAacEncoder.BuildAfconvertArgs("/in/a.wav", "/out/a.m4a.tmp", 160);
        Assert.AreEqual("m4af", a[a.IndexOf("-f") + 1]);
        Assert.AreEqual("aac", a[a.IndexOf("-d") + 1]);
        Assert.AreEqual("160000", a[a.IndexOf("-b") + 1], "afconvert 는 bps 단위");
        Assert.AreEqual("/in/a.wav", a[a.Count - 2]);
        Assert.AreEqual("/out/a.m4a.tmp", a[a.Count - 1]);
    }

    [Test]
    public void EnumerateFfmpegCandidates_EnvFirst_ThenPath_ThenKnownDirs()
    {
        var c = AITAudioAacEncoder.EnumerateFfmpegCandidates("/custom/ffmpeg", "/a/bin:/b/bin", false);
        Assert.AreEqual("/custom/ffmpeg", c[0]);
        Assert.AreEqual(System.IO.Path.Combine("/a/bin", "ffmpeg"), c[1]);
        Assert.AreEqual(System.IO.Path.Combine("/b/bin", "ffmpeg"), c[2]);
        CollectionAssert.Contains(c, "/opt/homebrew/bin/ffmpeg", "Finder 로 뜬 에디터는 PATH 가 최소라 흔한 위치도 본다");

        var w = AITAudioAacEncoder.EnumerateFfmpegCandidates(null, "C:\\tools;\"C:\\ff mpeg\"", true);
        Assert.AreEqual(2, w.Count, "Windows 는 환경 변수 미지정 시 PATH 항목만");
        StringAssert.EndsWith("ffmpeg.exe", w[0]);
    }

    [Test]
    public void DecideAacSkipReason_Eligible_ReturnsNull()
    {
        Assert.IsNull(AITAudioStreamTranscoder.DecideAacSkipReason(".wav", true, true, 2, false, false, false));
        Assert.IsNull(AITAudioStreamTranscoder.DecideAacSkipReason(".WAV", true, true, 1, false, false, false), "확장자 대소문자 무관");
    }

    [TestCase(".mp3", true, true, 2, "not-wav")]
    [TestCase(".ogg", true, true, 2, "not-wav")]
    [TestCase(".wav", false, true, 2, "not-compressed-path")]  // 짧은 효과음: 지연 없는 PCM 유지
    [TestCase(".wav", true, false, 2, "not-pcm")]               // ADPCM 등
    [TestCase(".wav", true, true, 6, "channels")]               // 5.1
    [TestCase(".wav", true, true, 0, "channels")]
    public void DecideAacSkipReason_StaticRules(string ext, bool compressed, bool pcm, int ch, string expected)
    {
        Assert.AreEqual(expected, AITAudioStreamTranscoder.DecideAacSkipReason(ext, compressed, pcm, ch, false, false, false));
    }

    [Test]
    public void DecideAacSkipReason_LoopClip_NeedsOptIn()
    {
        Assert.AreEqual("loop", AITAudioStreamTranscoder.DecideAacSkipReason(".wav", true, true, 2, true, false, false),
            "auto(옵트인 없음)에서 루프 클립은 이음새 위험으로 제외");
        Assert.IsNull(AITAudioStreamTranscoder.DecideAacSkipReason(".wav", true, true, 2, true, false, true),
            "audioStreamLoopTranscode=1 이면 루프 클립도 변환");
    }

    [Test]
    public void DecideAacSkipReason_UnknownLoopState_NeedsOptIn()
    {
        // 바이너리 씬/프리팹이 있어 루프 여부를 확정할 수 없으면 옵트인 없이는 건드리지 않는다.
        Assert.AreEqual("loop-unknown", AITAudioStreamTranscoder.DecideAacSkipReason(".wav", true, true, 2, false, true, false));
        Assert.IsNull(AITAudioStreamTranscoder.DecideAacSkipReason(".wav", true, true, 2, false, true, true));
    }

    [Test]
    public void DecideAacSkipReason_DoesNotApplyMp3LengthGate()
    {
        // MP3 경로는 20초 초과 클립을 BGM 으로 보고 제외하지만 AAC 경로는 그 길이 게이트를 쓰지 않는다(쓰면 BGM 이득이 사라짐).
        // 시그니처에 길이가 없다는 것 자체가 계약이다 — 길이만으로는 제외 사유가 나오지 않는다.
        Assert.IsNull(AITAudioStreamTranscoder.DecideAacSkipReason(".wav", true, true, 2, false, false, false));
    }

    [Test]
    public void TranscodeWavToAac_Disabled_ReturnsEmptyAndTouchesNothing()
    {
        _config.audioStreamTranscode = 0;
        var cands = new System.Collections.Generic.List<AITAudioStreamTranscoder.Candidate>
        {
            new AITAudioStreamTranscoder.Candidate { AbsPath = "/nonexistent/a.wav", Bytes = 10000000, Seconds = 60f, Guid = "g1", Compressed = true },
        };
        Assert.AreEqual(0, AITAudioStreamTranscoder.TranscodeWavToAac(_config, cands).Count);
        Assert.AreEqual(0, AITAudioStreamTranscoder.TranscodeWavToAac(_config, null).Count);
    }

    [Test]
    public void TranscodeWavToAac_NonCompressedOrNonWav_AreNotCandidates()
    {
        _config.audioStreamTranscode = 1;
        var cands = new System.Collections.Generic.List<AITAudioStreamTranscoder.Candidate>
        {
            new AITAudioStreamTranscoder.Candidate { AbsPath = "/nonexistent/a.wav", Bytes = 10000000, Seconds = 60f, Guid = "g1", Compressed = false },
            new AITAudioStreamTranscoder.Candidate { AbsPath = "/nonexistent/b.mp3", Bytes = 10000000, Seconds = 60f, Guid = "g2", Compressed = true },
        };
        Assert.AreEqual(0, AITAudioStreamTranscoder.TranscodeWavToAac(_config, cands).Count);
    }

    [Test]
    public void AacConstants_MatchRuntimeContract()
    {
        Assert.AreEqual(".m4a", AITAudioStreamTranscoder.AacExtension);
        Assert.AreEqual("audio/mp4", AITAudioStreamTranscoder.AacMime);
        Assert.IsTrue(AITStreamingAudio.RequiresMediaElement("x" + AITAudioStreamTranscoder.AacExtension, null),
            "빌드가 내는 확장자는 런타임이 media element 전용으로 인식해야 한다");
        Assert.AreEqual(AudioType.ACC, AITStreamingAudio.GuessAudioType("x" + AITAudioStreamTranscoder.AacExtension));
    }

    // ─────────────────────────── 매니페스트 entry 직렬화 (file/mime 반영) ───────────────────────────

    [Test]
    public void EntryRecord_ToJson_OmitsMimeWhenNotTranscoded_AndKeepsOldShape()
    {
        var r = new AITAudioStreamingProcessor.EntryRecord { Guid = "abc", Name = "bgm", FileName = "abc.wav", Length = 60f, Compressed = true };
        Assert.AreEqual("{\"guid\":\"abc\",\"name\":\"bgm\",\"file\":\"abc.wav\",\"length\":60,\"compressed\":true}", r.ToJson());
    }

    [Test]
    public void EntryRecord_ToJson_WritesMimeAfterTranscode_AndRoundTripsThroughRuntimeManifest()
    {
        var r = new AITAudioStreamingProcessor.EntryRecord { Guid = "abc", Name = "bgm", FileName = "abc.m4a", Length = 60.5f, Compressed = true, Mime = AITAudioStreamTranscoder.AacMime };
        string json = "{\"entries\":[" + r.ToJson() + "]}";
        StringAssert.Contains("\"file\":\"abc.m4a\"", json);
        StringAssert.Contains("\"mime\":\"audio/mp4\"", json);

        var manifestType = typeof(AITStreamingAudio).GetNestedType("Manifest", System.Reflection.BindingFlags.NonPublic);
        var entryType = typeof(AITStreamingAudio).GetNestedType("Entry", System.Reflection.BindingFlags.NonPublic);
        object manifest = JsonUtility.FromJson(json, manifestType);
        var entries = (System.Array)manifestType.GetField("entries").GetValue(manifest);
        Assert.AreEqual(1, entries.Length);
        object e = entries.GetValue(0);
        Assert.AreEqual("abc.m4a", entryType.GetField("file").GetValue(e));
        Assert.AreEqual("audio/mp4", entryType.GetField("mime").GetValue(e));
        Assert.AreEqual(true, entryType.GetField("compressed").GetValue(e));
    }
}
