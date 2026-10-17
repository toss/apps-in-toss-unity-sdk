// -----------------------------------------------------------------------
// AITAudioReencodeProcessorTests.cs - 오디오 재인코딩 판정 순수 로직 검증
// Level 0: NeedsReencode(현재 포맷/품질, target, explicitMode) → 재인코딩 필요 여부
//
// 핵심 불변식:
//   1) 비압축(PCM/ADPCM)은 자동·explicit 공통으로 Vorbis 로 변환(가장 큰 이득).
//   2) 이미 Vorbis 인 클립은 자동 모드에서 절대 건드리지 않는다(세대손실 방지, 작성자 의도 존중).
//   3) explicit 모드에서만 기존 Vorbis 를 target 초과 시 낮춘다(target 이하는 불변).
//   4) MP3/AAC 등 기타 압축 포맷은 어느 모드에서도 건드리지 않는다.
// -----------------------------------------------------------------------

using NUnit.Framework;
using UnityEngine;
using AppsInToss.Editor;

[TestFixture]
public class AITAudioReencodeProcessorTests
{
    // ─────────────── 비압축(PCM/ADPCM) → 항상 변환 ───────────────

    [Test]
    public void NeedsReencode_PCM_Auto_AlwaysTrue()
    {
        Assert.IsTrue(AITAudioReencodeProcessor.NeedsReencode(AudioCompressionFormat.PCM, 1f, 0.7f, false),
            "비압축 PCM 은 자동 모드에서도 Vorbis 로 변환해야 한다.");
    }

    [Test]
    public void NeedsReencode_PCM_Explicit_AlwaysTrue()
    {
        Assert.IsTrue(AITAudioReencodeProcessor.NeedsReencode(AudioCompressionFormat.PCM, 1f, 0.7f, true));
    }

    [Test]
    public void NeedsReencode_ADPCM_Auto_True()
    {
        Assert.IsTrue(AITAudioReencodeProcessor.NeedsReencode(AudioCompressionFormat.ADPCM, 0.5f, 0.7f, false),
            "ADPCM(경량 압축)도 Vorbis 로 변환 대상이다.");
    }

    // ─────────────── 이미 Vorbis: 자동은 불변, explicit 만 축소 ───────────────

    [Test]
    public void NeedsReencode_VorbisAboveTarget_Auto_False()
    {
        // 자동 모드는 기존 Vorbis 를 건드리지 않는다(세대손실 방지) — quality 가 target 보다 높아도.
        Assert.IsFalse(AITAudioReencodeProcessor.NeedsReencode(AudioCompressionFormat.Vorbis, 0.9f, 0.7f, false),
            "자동 모드는 이미 Vorbis 인 클립을 절대 재인코딩하지 않아야 한다.");
    }

    [Test]
    public void NeedsReencode_VorbisAboveTarget_Explicit_True()
    {
        // explicit 모드에서만 target 초과 Vorbis 를 낮춘다.
        Assert.IsTrue(AITAudioReencodeProcessor.NeedsReencode(AudioCompressionFormat.Vorbis, 0.9f, 0.7f, true),
            "explicit 모드는 target 초과 Vorbis 를 낮춰야 한다.");
    }

    [Test]
    public void NeedsReencode_VorbisBelowTarget_Explicit_False()
    {
        // 이미 target 이하 → 더 낮추지 않음(불필요한 재인코딩 회피).
        Assert.IsFalse(AITAudioReencodeProcessor.NeedsReencode(AudioCompressionFormat.Vorbis, 0.5f, 0.7f, true),
            "이미 target 이하인 Vorbis 는 건드리지 않아야 한다.");
    }

    [Test]
    public void NeedsReencode_VorbisEqualTarget_Explicit_False()
    {
        // 동일 quality → no-op(eps 이내).
        Assert.IsFalse(AITAudioReencodeProcessor.NeedsReencode(AudioCompressionFormat.Vorbis, 0.7f, 0.7f, true));
    }

    [Test]
    public void NeedsReencode_VorbisWithinEpsilon_Explicit_False()
    {
        // target+eps(0.001) 이내의 미세 초과는 무시(부동소수 잡음/불필요 재인코딩 방지).
        Assert.IsFalse(AITAudioReencodeProcessor.NeedsReencode(AudioCompressionFormat.Vorbis, 0.7001f, 0.7f, true),
            "eps 이내 미세 초과는 재인코딩하지 않아야 한다.");
    }

    [Test]
    public void NeedsReencode_VorbisClearlyAbove_Explicit_True()
    {
        // eps 초과의 유의미한 차이는 낮춘다.
        Assert.IsTrue(AITAudioReencodeProcessor.NeedsReencode(AudioCompressionFormat.Vorbis, 0.71f, 0.7f, true));
    }

    // ─────────────── 기타 압축 포맷(MP3 등)은 불변 ───────────────

    [Test]
    public void NeedsReencode_MP3_Auto_False()
    {
        Assert.IsFalse(AITAudioReencodeProcessor.NeedsReencode(AudioCompressionFormat.MP3, 1f, 0.7f, false),
            "MP3(기타 압축)는 자동 모드에서 건드리지 않아야 한다(세대손실 방지).");
    }

    [Test]
    public void NeedsReencode_MP3_Explicit_False()
    {
        // explicit 모드도 MP3 는 낮추지 않는다(explicit 축소는 Vorbis 한정).
        Assert.IsFalse(AITAudioReencodeProcessor.NeedsReencode(AudioCompressionFormat.MP3, 1f, 0.7f, true),
            "MP3 는 explicit 모드에서도 건드리지 않아야 한다(Vorbis 만 축소 대상).");
    }
}
