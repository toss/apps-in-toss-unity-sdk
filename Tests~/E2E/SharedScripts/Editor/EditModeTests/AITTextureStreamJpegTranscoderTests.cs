// -----------------------------------------------------------------------
// AITTextureStreamJpegTranscoderTests.cs - 불투명 스트림 PNG → JPEG 전환 판정 규칙 검증
// Level 0: IsEnabled / ResolveQuality / ShouldAdopt / IsOpaquePng (순수 함수)
// + TranscodeInPlace 파일 왕복(임시 디렉토리, 외부 도구 불필요 — 순수 C#)
//
// 핵심 불변식:
//   1) lossy 레버라 auto(-1) 기본 비활성 — 무손실 레버(textureStreamRecompress, 기본 ON)와
//      의도적으로 다른 posture. 시각 검증 게이트 통과 후 기본값을 바꾸면 이 테스트와 문서를 함께 갱신할 것.
//   2) 채택 게이트: ≥25% 이득(AITBrotliCompressor.ShouldKeep)일 때만 교체 — lossy 전환은
//      경계 이득에서 원본 PNG 유지.
//   3) 불투명 판정은 '확실히 불투명'(IHDR colortype==2 RGB & tRNS 없음)만 true —
//      gray/palette 는 마스크·플랫 아트 가능성으로 보수적 제외.
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using AppsInToss;
using AppsInToss.Editor;

[TestFixture]
public class AITTextureStreamJpegTranscoderTests
{
    private AITEditorScriptObject _config;
    private string _tmpDir;

    [SetUp]
    public void SetUp()
    {
        _config = ScriptableObject.CreateInstance<AITEditorScriptObject>();
        _tmpDir = Path.Combine(Path.GetTempPath(), "ait-jpeg-transcode-test-" + Path.GetRandomFileName());
        Directory.CreateDirectory(_tmpDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (_config != null)
        {
            Object.DestroyImmediate(_config);
        }

        if (Directory.Exists(_tmpDir))
        {
            Directory.Delete(_tmpDir, true);
        }
    }

    // ─────────────────────────── IsEnabled (tri-state) ───────────────────────────

    [Test]
    public void IsEnabled_NullConfig_ReturnsFalse()
    {
        Assert.IsFalse(AITTextureStreamJpegTranscoder.IsEnabled(null));
    }

    [Test]
    public void IsEnabled_Auto_FollowsSdkDefaultOff()
    {
        _config.textureStreamJpeg = -1;
        Assert.IsFalse(AITDefaultSettings.GetDefaultTextureStreamJpeg(),
            "lossy 레버는 시각 검증 게이트 전 auto 기본 OFF 여야 한다 — 켜려면 이 테스트와 문서를 함께 갱신할 것");
        Assert.IsFalse(AITTextureStreamJpegTranscoder.IsEnabled(_config));
    }

    [Test]
    public void IsEnabled_ExplicitOff_ReturnsFalse()
    {
        _config.textureStreamJpeg = 0;
        Assert.IsFalse(AITTextureStreamJpegTranscoder.IsEnabled(_config));
    }

    [Test]
    public void IsEnabled_ExplicitOn_ReturnsTrue()
    {
        _config.textureStreamJpeg = 1;
        Assert.IsTrue(AITTextureStreamJpegTranscoder.IsEnabled(_config));
    }

    // ─────────────────────────── ResolveQuality ───────────────────────────

    [Test]
    public void ResolveQuality_DefaultAndInvalid_Returns90()
    {
        Assert.AreEqual(90, AITTextureStreamJpegTranscoder.ResolveQuality(null));
        _config.textureStreamJpegQuality = 0;
        Assert.AreEqual(90, AITTextureStreamJpegTranscoder.ResolveQuality(_config));
        _config.textureStreamJpegQuality = -7;
        Assert.AreEqual(90, AITTextureStreamJpegTranscoder.ResolveQuality(_config));
    }

    [Test]
    public void ResolveQuality_ClampsToRange()
    {
        _config.textureStreamJpegQuality = 10;
        Assert.AreEqual(50, AITTextureStreamJpegTranscoder.ResolveQuality(_config), "하한 50 클램프");
        _config.textureStreamJpegQuality = 250;
        Assert.AreEqual(100, AITTextureStreamJpegTranscoder.ResolveQuality(_config), "상한 100 클램프");
        _config.textureStreamJpegQuality = 85;
        Assert.AreEqual(85, AITTextureStreamJpegTranscoder.ResolveQuality(_config), "정상 범위는 그대로");
    }

    // ─────────────────────────── ShouldAdopt (≥25% 게이트) ───────────────────────────

    [Test]
    public void ShouldAdopt_GainAtOrAboveThreshold_ReturnsTrue()
    {
        Assert.IsTrue(AITTextureStreamJpegTranscoder.ShouldAdopt(100L, 75L), "정확히 25% 이득은 채택");
        Assert.IsTrue(AITTextureStreamJpegTranscoder.ShouldAdopt(15_700_000L, 3_560_000L), "실측 대표값(−77%)");
    }

    [Test]
    public void ShouldAdopt_MarginalGain_ReturnsFalse()
    {
        Assert.IsFalse(AITTextureStreamJpegTranscoder.ShouldAdopt(100L, 76L), "25% 미만 이득은 원본 유지");
        Assert.IsFalse(AITTextureStreamJpegTranscoder.ShouldAdopt(100L, 100L));
        Assert.IsFalse(AITTextureStreamJpegTranscoder.ShouldAdopt(100L, 120L));
    }

    [Test]
    public void ShouldAdopt_InvalidSizes_ReturnsFalse()
    {
        Assert.IsFalse(AITTextureStreamJpegTranscoder.ShouldAdopt(0L, 0L));
        Assert.IsFalse(AITTextureStreamJpegTranscoder.ShouldAdopt(100L, 0L));
        Assert.IsFalse(AITTextureStreamJpegTranscoder.ShouldAdopt(-1L, 10L));
    }

    // ─────────────────────────── IsOpaquePng (헤더 파싱) ───────────────────────────

    // 파서는 CRC 를 검증하지 않으므로 더미 CRC 로 최소 PNG 를 손조립한다.
    private static byte[] BuildPngHeader(byte colorType, bool withTrns)
    {
        var bytes = new List<byte> { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        // IHDR: len=13, "IHDR", w=1,h=1,bit=8,color,comp=0,filter=0,interlace=0, dummy CRC
        bytes.AddRange(new byte[] { 0, 0, 0, 13, 0x49, 0x48, 0x44, 0x52 });
        bytes.AddRange(new byte[] { 0, 0, 0, 1, 0, 0, 0, 1, 8, colorType, 0, 0, 0 });
        bytes.AddRange(new byte[] { 0, 0, 0, 0 });
        if (withTrns)
        {
            // tRNS: len=6 (RGB 샘플 3개 x 2바이트), 더미 데이터/CRC
            bytes.AddRange(new byte[] { 0, 0, 0, 6, 0x74, 0x52, 0x4E, 0x53 });
            bytes.AddRange(new byte[] { 0, 0, 0, 0, 0, 0 });
            bytes.AddRange(new byte[] { 0, 0, 0, 0 });
        }

        // IDAT: len=0, 더미 CRC
        bytes.AddRange(new byte[] { 0, 0, 0, 0, 0x49, 0x44, 0x41, 0x54 });
        bytes.AddRange(new byte[] { 0, 0, 0, 0 });
        return bytes.ToArray();
    }

    [Test]
    public void IsOpaquePng_RgbWithoutTrns_ReturnsTrue()
    {
        Assert.IsTrue(AITTextureStreamJpegTranscoder.IsOpaquePng(BuildPngHeader(2, withTrns: false)));
    }

    [Test]
    public void IsOpaquePng_RgbWithTrns_ReturnsFalse()
    {
        Assert.IsFalse(AITTextureStreamJpegTranscoder.IsOpaquePng(BuildPngHeader(2, withTrns: true)),
            "tRNS 보유 RGB 는 투명 픽셀이 있으므로 제외");
    }

    [Test]
    public void IsOpaquePng_NonRgbColorTypes_ReturnFalse()
    {
        Assert.IsFalse(AITTextureStreamJpegTranscoder.IsOpaquePng(BuildPngHeader(6, withTrns: false)), "RGBA");
        Assert.IsFalse(AITTextureStreamJpegTranscoder.IsOpaquePng(BuildPngHeader(4, withTrns: false)), "grayA");
        Assert.IsFalse(AITTextureStreamJpegTranscoder.IsOpaquePng(BuildPngHeader(0, withTrns: false)), "gray — 마스크 가능성, 보수적 제외");
        Assert.IsFalse(AITTextureStreamJpegTranscoder.IsOpaquePng(BuildPngHeader(3, withTrns: false)), "palette — 플랫 아트, 보수적 제외");
    }

    [Test]
    public void IsOpaquePng_InvalidBytes_ReturnFalse()
    {
        Assert.IsFalse(AITTextureStreamJpegTranscoder.IsOpaquePng(null));
        Assert.IsFalse(AITTextureStreamJpegTranscoder.IsOpaquePng(new byte[0]));
        Assert.IsFalse(AITTextureStreamJpegTranscoder.IsOpaquePng(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }), "JPEG 매직");
        var truncated = new byte[20];
        System.Array.Copy(BuildPngHeader(2, withTrns: false), truncated, 20);
        Assert.IsFalse(AITTextureStreamJpegTranscoder.IsOpaquePng(truncated), "IHDR 도중 잘린 파일");
    }

    [Test]
    public void IsOpaquePng_MatchesUnityEncoder()
    {
        // Unity EncodeToPNG 실산출물과 파서의 정합: RGB24 → colortype 2(true), RGBA32 → 6(false).
        var rgb = new Texture2D(4, 4, TextureFormat.RGB24, false);
        var rgba = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        try
        {
            Assert.IsTrue(AITTextureStreamJpegTranscoder.IsOpaquePng(rgb.EncodeToPNG()));
            Assert.IsFalse(AITTextureStreamJpegTranscoder.IsOpaquePng(rgba.EncodeToPNG()));
        }
        finally
        {
            Object.DestroyImmediate(rgb);
            Object.DestroyImmediate(rgba);
        }
    }

    // ─────────────────────────── TranscodeInPlace (파일 왕복) ───────────────────────────

    // JPEG 가 확실히 이기는 소재: 다주파 사인 필드(사진류 근사) — 매끈해서 DCT 는 소수 계수로
    // 표현하지만 선형이 아니라 PNG 필터(Sub/Up)가 모델링하지 못한다(실측 PNG 47KB → JPEG q90 7.5KB).
    // ⚠ 선형 그라디언트를 쓰면 안 됨: PNG 필터가 완벽히 예측해 PNG 가 JPEG 보다 작아진다.
    private string WriteSineFieldPng(string name, bool withAlpha)
    {
        var tex = new Texture2D(128, 128, withAlpha ? TextureFormat.RGBA32 : TextureFormat.RGB24, false);
        try
        {
            for (int y = 0; y < 128; y++)
            {
                for (int x = 0; x < 128; x++)
                {
                    float r = 0.5f * (1f + Mathf.Sin(x * 0.23f + y * 0.11f));
                    float g = 0.5f * (1f + Mathf.Sin(x * 0.07f - y * 0.19f + 1.3f));
                    float b = 0.5f * (1f + Mathf.Sin((x + y) * 0.13f + 2.1f));
                    tex.SetPixel(x, y, new Color(r, g, b, withAlpha ? 0.5f : 1f));
                }
            }

            tex.Apply();
            string path = Path.Combine(_tmpDir, name);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            return path;
        }
        finally
        {
            Object.DestroyImmediate(tex);
        }
    }

    [Test]
    public void TranscodeInPlace_OpaquePng_ConvertsToJpgAndRenames()
    {
        _config.textureStreamJpeg = 1;
        string png = WriteSineFieldPng("opaque.png", withAlpha: false);
        var renamed = AITTextureStreamJpegTranscoder.TranscodeInPlace(_config, new[] { png });

        Assert.AreEqual(1, renamed.Count, "불투명 사인 필드는 ≥25% 이득으로 채택되어야 함");
        string jpg = renamed[png];
        Assert.AreEqual(Path.ChangeExtension(png, ".jpg"), jpg);
        Assert.IsFalse(File.Exists(png), "원본 PNG 는 제거");
        Assert.IsTrue(File.Exists(jpg), "JPEG 산출물 생성");
        var head = File.ReadAllBytes(jpg);
        Assert.IsTrue(head.Length > 2 && head[0] == 0xFF && head[1] == 0xD8, "JPEG 매직(SOI)");
    }

    [Test]
    public void TranscodeInPlace_AlphaPng_LeftUntouched()
    {
        _config.textureStreamJpeg = 1;
        string png = WriteSineFieldPng("alpha.png", withAlpha: true);
        long before = new FileInfo(png).Length;
        var renamed = AITTextureStreamJpegTranscoder.TranscodeInPlace(_config, new[] { png });

        Assert.AreEqual(0, renamed.Count);
        Assert.IsTrue(File.Exists(png), "알파 보유 PNG 는 그대로 유지");
        Assert.AreEqual(before, new FileInfo(png).Length, "바이트 불변");
    }

    [Test]
    public void TranscodeInPlace_Disabled_NoOp()
    {
        _config.textureStreamJpeg = 0;
        string png = WriteSineFieldPng("disabled.png", withAlpha: false);
        var renamed = AITTextureStreamJpegTranscoder.TranscodeInPlace(_config, new[] { png });

        Assert.AreEqual(0, renamed.Count);
        Assert.IsTrue(File.Exists(png), "비활성 시 어떤 파일도 건드리지 않음");
    }
}
