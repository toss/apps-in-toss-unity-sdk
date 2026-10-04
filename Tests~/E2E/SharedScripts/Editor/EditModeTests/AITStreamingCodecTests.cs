// -----------------------------------------------------------------------
// AITStreamingCodecTests.cs - 스트리밍 페이로드 brotli 코덱(AITStreamingCodec) 순수 로직 검증
// Level 0: ait-stream-* 의 .br 페이로드를 런타임에서 판별/해제하는 결정 로직 회귀 테스트.
//   - LooksLikePng/Jpg/Image/UnityFs : 매직 바이트 스니퍼(서버 해제 여부 역판별의 근거 — 오판 시
//                                       raw brotli 를 원본으로 오인해 디코드 파손)
//   - TryBrotliDecompress            : raw brotli 해제(프로파일 분기 — NET_STANDARD 만 가능)
//   - DecodePayload                  : 라우팅(비br passthrough / 서버-해제 passthrough / 클라 해제 / 실패 폴백)
//   - AITBrotliCompressor.ShouldKeep : 빌드타임 채택 임계(%) 판정
// 실제 네트워크 GET 은 브라우저(WebGL) 경로라 EditMode 로는 검증 불가 — E2E 가 부팅/스트리밍 재수화를
// 커버하고, 본 테스트는 그 외 순수 결정 로직을 결정론적으로 고정한다.
//
// 픽스처(payloadB64/brB64)는 node zlib.brotliCompressSync(q11) 로 생성했고, br 은 payload 로
// 정확히 라운드트립함을 사전 검증했다(브라우저/BCL BrotliStream 모두 동일 스트림 포맷).
// -----------------------------------------------------------------------

using System;
using NUnit.Framework;
using AppsInToss;
using AppsInToss.Editor;

[TestFixture]
public class AITStreamingCodecTests
{
    // "AIT streaming codec brotli fixture payload 0123456789 0123456789 0123456789" (75B)
    private const string PayloadB64 =
        "QUlUIHN0cmVhbWluZyBjb2RlYyBicm90bGkgZml4dHVyZSBwYXlsb2FkIDAxMjM0NTY3ODkgMDEyMzQ1Njc4OSAwMTIzNDU2Nzg5";

    // 위 payload 를 brotli(q11) 압축한 바이트(64B).
    private const string BrB64 =
        "G0oA+I3UWi04bTK3UaoXos8bTfFBYSAoY4JWElULUopEROkARuBmKBbxZWDqaLxlESY9uTxw6g8ZKG2s8yGmDA==";

    private static byte[] Payload => Convert.FromBase64String(PayloadB64);

    private static byte[] Br => Convert.FromBase64String(BrB64);

    // =====================================================
    // 매직 스니퍼 — 서버가 이미 해제했는지 역판별하는 기준
    // =====================================================

    [Test]
    public void LooksLikePng_TrueOnlyForPngSignature()
    {
        Assert.IsTrue(AITStreamingCodec.LooksLikePng(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A }));
        Assert.IsFalse(AITStreamingCodec.LooksLikePng(new byte[] { 0xFF, 0xD8, 0xFF }));       // JPG
        Assert.IsFalse(AITStreamingCodec.LooksLikePng(new byte[] { 0x89, 0x50 }));             // 너무 짧음
        Assert.IsFalse(AITStreamingCodec.LooksLikePng(null));
    }

    [Test]
    public void LooksLikeJpg_TrueOnlyForJpgSignature()
    {
        Assert.IsTrue(AITStreamingCodec.LooksLikeJpg(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }));
        Assert.IsFalse(AITStreamingCodec.LooksLikeJpg(new byte[] { 0x89, 0x50, 0x4E, 0x47 })); // PNG
        Assert.IsFalse(AITStreamingCodec.LooksLikeJpg(new byte[] { 0xFF, 0xD8 }));             // 너무 짧음
        Assert.IsFalse(AITStreamingCodec.LooksLikeJpg(null));
    }

    [Test]
    public void LooksLikeImage_TruePngOrJpg_FalseOtherwise()
    {
        Assert.IsTrue(AITStreamingCodec.LooksLikeImage(new byte[] { 0x89, 0x50, 0x4E, 0x47 }));
        Assert.IsTrue(AITStreamingCodec.LooksLikeImage(new byte[] { 0xFF, 0xD8, 0xFF }));
        Assert.IsFalse(AITStreamingCodec.LooksLikeImage(Br));      // raw brotli(0x1B 0x4A…)
        Assert.IsFalse(AITStreamingCodec.LooksLikeImage(Payload)); // 텍스트("AIT…")
    }

    [Test]
    public void LooksLikeUnityFs_TrueOnlyForUnityFsSignature()
    {
        byte[] unityFs = { (byte)'U', (byte)'n', (byte)'i', (byte)'t', (byte)'y', (byte)'F', (byte)'S', 0x00 };
        Assert.IsTrue(AITStreamingCodec.LooksLikeUnityFs(unityFs));

        byte[] noNul = { (byte)'U', (byte)'n', (byte)'i', (byte)'t', (byte)'y', (byte)'F', (byte)'S', 0x01 };
        Assert.IsFalse(AITStreamingCodec.LooksLikeUnityFs(noNul)); // 종결 NUL 아님
        Assert.IsFalse(AITStreamingCodec.LooksLikeUnityFs(Br));
        Assert.IsFalse(AITStreamingCodec.LooksLikeUnityFs(null));
    }

    // =====================================================
    // TryBrotliDecompress — raw brotli 해제(프로파일 분기)
    //   Editor 테스트 어셈블리는 NET_4_6 로 컴파일되지만, 호출 대상 AITStreamingCodec 은
    //   런타임(Helpers) 어셈블리라 별도 프로파일(NET_STANDARD)로 컴파일된다. 따라서 기대치는
    //   테스트의 #if 가 아니라 런타임의 CanDecompressBrotli 로 분기해야 한다.
    // =====================================================

    [Test]
    public void TryBrotliDecompress_NullOrEmpty_ReturnsFalse()
    {
        // #if 이전의 이른 반환이라 프로파일 무관하게 항상 false.
        Assert.IsFalse(AITStreamingCodec.TryBrotliDecompress(null, out _));
        Assert.IsFalse(AITStreamingCodec.TryBrotliDecompress(Array.Empty<byte>(), out _));
    }

    [Test]
    public void TryBrotliDecompress_ValidStream_MatchesProfileCapability()
    {
        bool ok = AITStreamingCodec.TryBrotliDecompress(Br, out byte[] decoded);
        if (AITStreamingCodec.CanDecompressBrotli)
        {
            Assert.IsTrue(ok, "해제 가능 프로파일은 유효 brotli 를 true 로 해제해야 한다");
            Assert.AreEqual(Payload, decoded, "해제 결과가 원본 payload 와 바이트 단위로 일치해야 한다");
        }
        else
        {
            // BrotliStream 부재 프로파일: 항상 false(서버 Content-Encoding 경로만 유효).
            Assert.IsFalse(ok, "해제 불가 프로파일은 false 여야 한다");
        }
    }

    // =====================================================
    // DecodePayload — 라우팅 4분기
    // =====================================================

    [Test]
    public void DecodePayload_NonBrEncoding_ReturnsInputUnchanged()
    {
        byte[] data = Payload;
        // 무압축 엔트리(encoding=="" 또는 null)는 어떤 경우에도 원본 그대로.
        Assert.AreSame(data, AITStreamingCodec.DecodePayload("", data, AITStreamingCodec.LooksLikeImage, "t"));
        Assert.AreSame(data, AITStreamingCodec.DecodePayload(null, data, AITStreamingCodec.LooksLikeImage, "t"));
    }

    [Test]
    public void DecodePayload_NullData_ReturnsNull()
    {
        Assert.IsNull(AITStreamingCodec.DecodePayload("br", null, AITStreamingCodec.LooksLikeImage, "t"));
    }

    [Test]
    public void DecodePayload_ServerAlreadyDecoded_ReturnsInputUnchanged()
    {
        // 경로 1: 서버가 Content-Encoding: br 로 이미 해제 → 수신 바이트가 이미 기대 포맷(PNG).
        // 매직이 통과하므로 brotli 해제를 시도하지 않고 그대로 반환.
        byte[] png = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        Assert.AreSame(png, AITStreamingCodec.DecodePayload("br", png, AITStreamingCodec.LooksLikeImage, "t"));
    }

    [Test]
    public void DecodePayload_RawBrotli_MatchesProfileCapability()
    {
        // 경로 2: 서버 헤더 없이 raw brotli 도착. looksDecoded==null 로 포맷 검증을 생략하고 해제만 검증.
        byte[] input = Br;
        byte[] result = AITStreamingCodec.DecodePayload("br", input, null, "t");
        if (AITStreamingCodec.CanDecompressBrotli)
        {
            Assert.AreEqual(Payload, result, "raw brotli 는 해제되어 원본 payload 를 반환해야 한다");
        }
        else
        {
            // 해제 불가 프로파일: 원본(br) 그대로 반환(경고는 warning 이라 테스트를 실패시키지 않음).
            Assert.AreSame(input, result, "해제 불가 프로파일은 수신 br 바이트를 그대로 반환해야 한다");
        }
    }

    [Test]
    public void DecodePayload_DecodedButUnrecognized_FallsBackToOriginal()
    {
        // 안전망: 해제가 성공하더라도 결과가 기대 포맷(매직)을 만족하지 못하면 원본으로 폴백.
        // looksDecoded 를 항상 false 로 주어, 해제 성공/실패와 무관하게 폴백 계약을 결정론적으로 고정.
        byte[] input = Br;
        byte[] result = AITStreamingCodec.DecodePayload("br", input, _ => false, "t");
        Assert.AreSame(input, result, "해제 결과가 기대 포맷을 만족하지 못하면 원본 바이트로 폴백해야 한다");
    }

    // =====================================================
    // AITBrotliCompressor.ShouldKeep — 빌드타임 채택 임계(%)
    // =====================================================

    [Test]
    public void ShouldKeep_TrueOnlyWhenGainMeetsThreshold()
    {
        // 정확히 10% 이득(100→90): 임계 이상 → 채택.
        Assert.IsTrue(AITBrotliCompressor.ShouldKeep(100, 90, 10));
        // 10% 초과(100→80): 채택.
        Assert.IsTrue(AITBrotliCompressor.ShouldKeep(100, 80, 10));
        // 10% 미만(100→91): 미달 → 폐기.
        Assert.IsFalse(AITBrotliCompressor.ShouldKeep(100, 91, 10));
        // 오히려 커짐(100→105): 폐기.
        Assert.IsFalse(AITBrotliCompressor.ShouldKeep(100, 105, 10));
    }

    [Test]
    public void ShouldKeep_NonPositiveInputs_ReturnFalse()
    {
        Assert.IsFalse(AITBrotliCompressor.ShouldKeep(0, 0, 10));
        Assert.IsFalse(AITBrotliCompressor.ShouldKeep(100, 0, 10));
        Assert.IsFalse(AITBrotliCompressor.ShouldKeep(0, 50, 10));
        Assert.IsFalse(AITBrotliCompressor.ShouldKeep(-1, 50, 10));
    }

    [Test]
    public void DefaultMinGainPercent_IsTenPercent()
    {
        Assert.AreEqual(10, AITBrotliCompressor.DefaultMinGainPercent);
    }
}
