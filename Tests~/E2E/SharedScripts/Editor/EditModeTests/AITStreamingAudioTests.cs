// -----------------------------------------------------------------------
// AITStreamingAudioTests.cs - 런타임 오디오 재수화(AITStreamingAudio) 순수 로직 검증
// Level 0: 외부화 오디오를 런타임에 스트리밍 복원하는 컴포넌트의 결정 로직 회귀 테스트.
//   - GuessAudioType: 확장자→AudioType 매핑(오타 시 디코드 실패로 오디오가 조용히 사라짐 — 핵심 가드)
//   - IsStubLength  : 무음 스텁 길이 임계값 판정(스왑 대상 결정)
//   - JoinUrl       : StreamingAssets URL 결합(슬래시 중복 방지)
//   - HotSwap       : 스텁 AudioSource를 실 클립으로 교체(클립 참조 스왑 + 동일 클립 no-op)
// 실제 네트워크 GET/디코드는 브라우저(WebGL) 경로라 EditMode로는 검증 불가 — E2E가 부팅/매니페스트
// 로드/no-op 안전을 커버하고, 본 테스트는 그 외 순수 결정 로직을 결정론적으로 고정한다.
// -----------------------------------------------------------------------

using NUnit.Framework;
using UnityEngine;
using AppsInToss;

[TestFixture]
public class AITStreamingAudioTests
{
    // =====================================================
    // GuessAudioType — 확장자 매핑 (오타 = 무음, 최우선 가드)
    // =====================================================

    [TestCase("track.mp3", AudioType.MPEG)]
    [TestCase("track.MP3", AudioType.MPEG)]            // 대소문자 무관
    [TestCase("a/b/c/track.mp3", AudioType.MPEG)]      // 경로 포함
    [TestCase("bgm.ogg", AudioType.OGGVORBIS)]
    [TestCase("bgm.OGG", AudioType.OGGVORBIS)]
    [TestCase("sfx.wav", AudioType.WAV)]
    [TestCase("voice.aiff", AudioType.AIFF)]
    [TestCase("voice.aif", AudioType.AIFF)]
    [TestCase("data.bin", AudioType.UNKNOWN)]          // 미지원 확장자
    [TestCase("noext", AudioType.UNKNOWN)]
    public void GuessAudioType_MapsExtensionToAudioType(string file, AudioType expected)
    {
        Assert.AreEqual(expected, AITStreamingAudio.GuessAudioType(file));
    }

    // =====================================================
    // IsStubLength — 무음 스텁 판정(임계값 0.5s, 경계 포함)
    // =====================================================

    [TestCase(0.0f, true)]
    [TestCase(0.1f, true)]
    [TestCase(0.49f, true)]
    [TestCase(0.5f, false)]   // 경계: '< 0.5' 이므로 0.5 는 실 클립
    [TestCase(0.6f, false)]
    [TestCase(74.1f, false)]  // 실제 BGM 길이대
    public void IsStubLength_TrueOnlyBelowThreshold(float length, bool expected)
    {
        Assert.AreEqual(expected, AITStreamingAudio.IsStubLength(length));
    }

    // =====================================================
    // JoinUrl — StreamingAssets 경로 결합(슬래시 중복 방지)
    // =====================================================

    [Test]
    public void JoinUrl_AddsSingleSlash_WhenBaseHasNoTrailingSlash()
    {
        Assert.AreEqual(
            "https://cdn/web/StreamingAssets/ait-stream-audio/manifest.json",
            AITStreamingAudio.JoinUrl("https://cdn/web/StreamingAssets", "ait-stream-audio/manifest.json"));
    }

    [Test]
    public void JoinUrl_NoDoubleSlash_WhenBaseHasTrailingSlash()
    {
        Assert.AreEqual(
            "file:///p/StreamingAssets/ait-stream-audio/x.mp3",
            AITStreamingAudio.JoinUrl("file:///p/StreamingAssets/", "ait-stream-audio/x.mp3"));
    }

    // =====================================================
    // HotSwap — 스텁 AudioSource를 실 클립으로 교체 + 동일 클립 no-op
    // (EditMode: isPlaying=false 이므로 재생 분기는 미실행, 클립 참조 스왑만 검증)
    // =====================================================

    [Test]
    public void HotSwap_ReplacesStubClipWithRealClip()
    {
        var go = new GameObject("ait-test-audio");
        try
        {
            var src = go.AddComponent<AudioSource>();
            var stub = AudioClip.Create("BGM1", 4410, 1, 44100, false);   // ~0.1s 스텁
            var real = AudioClip.Create("BGM1", 88200, 1, 44100, false);  // 2.0s 실 클립
            src.clip = stub;

            AITStreamingAudio.HotSwap(src, real);
            Assert.AreSame(real, src.clip, "스텁이 실 클립으로 교체되어야 한다");

            // 동일 클립으로 재호출 시 no-op (참조 유지, 예외 없음)
            AITStreamingAudio.HotSwap(src, real);
            Assert.AreSame(real, src.clip, "동일 클립 재호출은 no-op 이어야 한다");

            Object.DestroyImmediate(stub);
            Object.DestroyImmediate(real);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }
}
