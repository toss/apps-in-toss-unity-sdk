// -----------------------------------------------------------------------
// AITDataRawSizeTests.cs - .data 압축 해제 크기 측정(AITDataRawSize) 검증
//   · 파일명 접미사 판정(Classify): .br / .gz / .unityweb / 무압축
//   · Node 러너 출력 파싱(TryParseRunnerOutput)
//   · Measure: 무압축=파일 길이, gzip=해제 길이, brotli=해제 길이(내장 Node 필요, 없으면 Ignore),
//             .unityweb·없는 파일·빈 경로·손상된 .br 은 -1(예외 없음)
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using NUnit.Framework;
using UnityEngine;
using AppsInToss;
using AppsInToss.Editor;

[TestFixture]
[Category("Unit")]
public class AITDataRawSizeTests
{
    // 1MB 를 넘는 비정렬 크기. 압축이 잘 되도록 반복 패턴 + 약간의 잡음.
    private const int RawLength = 3 * 1024 * 1024 + 17;

    private string _tempDir;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ait-datarawsize-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            if (!string.IsNullOrEmpty(_tempDir) && Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }
        catch { /* best-effort 정리 */ }
    }

    private static byte[] MakeRaw(int length)
    {
        var rng = new System.Random(1234);
        var bytes = new byte[length];
        for (int i = 0; i < length; i++)
        {
            bytes[i] = (byte)((i % 251) ^ (rng.Next(64) == 0 ? rng.Next(256) : 0));
        }

        return bytes;
    }

    // ─────────────────────────── Classify ───────────────────────────

    [Test]
    public void Classify_DetectsEncodingFromSuffix()
    {
        Assert.AreEqual(AITDataRawSize.DataEncoding.Brotli, AITDataRawSize.Classify("h.data.br"));
        Assert.AreEqual(AITDataRawSize.DataEncoding.Brotli, AITDataRawSize.Classify("H.DATA.BR"));
        Assert.AreEqual(AITDataRawSize.DataEncoding.Gzip, AITDataRawSize.Classify("h.data.gz"));
        Assert.AreEqual(AITDataRawSize.DataEncoding.Unityweb, AITDataRawSize.Classify("h.data.unityweb"));
        Assert.AreEqual(AITDataRawSize.DataEncoding.None, AITDataRawSize.Classify("h.data"));
        Assert.AreEqual(AITDataRawSize.DataEncoding.None, AITDataRawSize.Classify(""));
        Assert.AreEqual(AITDataRawSize.DataEncoding.None, AITDataRawSize.Classify(null));
    }

    [Test]
    public void Classify_UsesOnlyTrailingSuffix()
    {
        // 패치 접미사(.aitpN)가 역할 확장자 앞에 붙어도(h.aitp1.data.br) 끝 접미사로 판정한다.
        Assert.AreEqual(AITDataRawSize.DataEncoding.Brotli, AITDataRawSize.Classify("h.aitp1.data.br"));
        // 이름 중간의 .br 은 무관.
        Assert.AreEqual(AITDataRawSize.DataEncoding.None, AITDataRawSize.Classify("a.br.data"));
        // .unityweb 이 중간에 있어도 끝이 .br 이면 brotli(재압축 가드와 달리 측정은 끝 접미사 기준).
        Assert.AreEqual(AITDataRawSize.DataEncoding.Brotli, AITDataRawSize.Classify("a.unityweb.br"));
    }

    // ─────────────────────────── TryParseRunnerOutput ───────────────────────────

    [Test]
    public void TryParseRunnerOutput_RejectsNull()
    {
        Assert.IsFalse(AITDataRawSize.TryParseRunnerOutput(null, out long raw));
        Assert.AreEqual(-1L, raw);
    }

    [Test]
    public void TryParseRunnerOutput_ParsesRawLine()
    {
        Assert.IsTrue(AITDataRawSize.TryParseRunnerOutput("RAW 123456", out long raw));
        Assert.AreEqual(123456L, raw);

        Assert.IsTrue(AITDataRawSize.TryParseRunnerOutput("  RAW 0\n", out raw));
        Assert.AreEqual(0L, raw);

        // 4GB 를 넘는 값도 long 으로 파싱된다.
        Assert.IsTrue(AITDataRawSize.TryParseRunnerOutput("RAW 5000000000", out raw));
        Assert.AreEqual(5000000000L, raw);
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("RAW")]
    [TestCase("RAW abc")]
    [TestCase("RAW -5")]
    [TestCase("RAW 1.5")]
    [TestCase("raw 10")]
    [TestCase("DECODE_ERR boom")]
    public void TryParseRunnerOutput_RejectsMalformed(string stdout)
    {
        Assert.IsFalse(AITDataRawSize.TryParseRunnerOutput(stdout, out long raw));
        Assert.AreEqual(-1L, raw);
    }

    // ─────────────────────────── Measure ───────────────────────────

    [Test]
    public void Measure_ReturnsMinusOne_ForMissingOrEmptyPath()
    {
        Assert.AreEqual(-1L, AITDataRawSize.Measure(null));
        Assert.AreEqual(-1L, AITDataRawSize.Measure(""));
        Assert.AreEqual(-1L, AITDataRawSize.Measure(Path.Combine(_tempDir, "nope.data.br")));
    }

    [Test]
    public void Measure_ReturnsMinusOne_ForUnityweb()
    {
        string path = Path.Combine(_tempDir, "h.data.unityweb");
        File.WriteAllBytes(path, MakeRaw(4096));
        Assert.AreEqual(-1L, AITDataRawSize.Measure(path));
    }

    [Test]
    public void Measure_Uncompressed_ReturnsFileLength()
    {
        string path = Path.Combine(_tempDir, "h.data");
        File.WriteAllBytes(path, MakeRaw(RawLength));
        Assert.AreEqual((long)RawLength, AITDataRawSize.Measure(path));
    }

    [Test]
    public void Measure_Gzip_ReturnsDecompressedLength()
    {
        string path = Path.Combine(_tempDir, "h.data.gz");
        byte[] raw = MakeRaw(RawLength);
        using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
        using (var gz = new GZipStream(fs, CompressionMode.Compress))
        {
            gz.Write(raw, 0, raw.Length);
        }

        Assert.Less(new FileInfo(path).Length, (long)RawLength, "픽스처가 실제로 압축돼야 한다.");
        Assert.AreEqual((long)RawLength, AITDataRawSize.Measure(path));
    }

    [Test]
    public void Measure_CorruptGzip_ReturnsMinusOne()
    {
        string path = Path.Combine(_tempDir, "h.data.gz");
        File.WriteAllText(path, "this is not gzip");
        Assert.AreEqual(-1L, AITDataRawSize.Measure(path));
    }

    [Test]
    public void Measure_Brotli_ReturnsDecompressedLength()
    {
        // 내장 Node 로 .br 픽스처를 만든다(AITBrotliCompressor.Compress 는 "<원본>.br" 사본을 만든다).
        // Node 미가용(오프라인 batchmode 등)이면 건너뛴다 — AITBrotliRecompressTests 와 같은 컨벤션.
        if (!AITBrotliCompressor.TryResolveNode(out string node) || string.IsNullOrEmpty(node))
        {
            Assert.Ignore("내장 Node 미가용 — brotli 측정 테스트 건너뜀");
        }

        string rawPath = Path.Combine(_tempDir, "h.data");
        File.WriteAllBytes(rawPath, MakeRaw(RawLength));

        var results = AITBrotliCompressor.Compress(new List<string> { rawPath });
        if (!results.TryGetValue(rawPath, out var r) || r == null || !r.Ok)
        {
            Assert.Ignore("brotli 픽스처 생성 실패 — brotli 측정 테스트 건너뜀");
        }

        string brPath = rawPath + ".br";
        Assert.IsTrue(File.Exists(brPath), ".br 픽스처가 있어야 한다.");
        Assert.Less(new FileInfo(brPath).Length, (long)RawLength, "픽스처가 실제로 압축돼야 한다(Content-Length 와 RAW 가 달라야 의미가 있다).");

        Assert.AreEqual((long)RawLength, AITDataRawSize.Measure(brPath));
    }

    [Test]
    public void Measure_CorruptBrotli_ReturnsMinusOne()
    {
        if (!AITBrotliCompressor.TryResolveNode(out string node) || string.IsNullOrEmpty(node))
        {
            Assert.Ignore("내장 Node 미가용 — brotli 측정 테스트 건너뜀");
        }

        string path = Path.Combine(_tempDir, "h.data.br");
        File.WriteAllText(path, "this is not brotli");
        Assert.AreEqual(-1L, AITDataRawSize.Measure(path));
    }

    [Test]
    public void Measure_TruncatedBrotli_ReturnsMinusOne()
    {
        if (!AITBrotliCompressor.TryResolveNode(out string node) || string.IsNullOrEmpty(node))
        {
            Assert.Ignore("내장 Node 미가용 — brotli 측정 테스트 건너뜀");
        }

        // 잡음이 많은 입력은 잘 압축되지 않아 끝을 자르면 반드시 불완전한 스트림이 된다.
        var rng = new System.Random(99);
        var noise = new byte[256 * 1024];
        rng.NextBytes(noise);
        string rawPath = Path.Combine(_tempDir, "n.data");
        File.WriteAllBytes(rawPath, noise);

        var results = AITBrotliCompressor.Compress(new List<string> { rawPath });
        if (!results.TryGetValue(rawPath, out var r) || r == null || !r.Ok)
        {
            Assert.Ignore("brotli 픽스처 생성 실패 — 건너뜀");
        }

        byte[] br = File.ReadAllBytes(rawPath + ".br");
        string truncated = Path.Combine(_tempDir, "trunc.data.br");
        var cut = new byte[br.Length - 4096];
        Array.Copy(br, cut, cut.Length);
        File.WriteAllBytes(truncated, cut);

        Assert.AreEqual(-1L, AITDataRawSize.Measure(truncated), "잘린 .br 은 크기를 단정하지 않고 -1 이어야 한다.");
    }
}
