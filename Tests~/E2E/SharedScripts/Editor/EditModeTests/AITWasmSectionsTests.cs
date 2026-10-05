// -----------------------------------------------------------------------
// AITWasmSectionsTests.cs - wasm 섹션 크기 파서(AITWasmSections) 검증
// Level 0: 합성 wasm 으로 LEB128 크기, code/data/custom 합산, 잘못된 입력 거부를 고정한다.
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using NUnit.Framework;
using AppsInToss.Editor;

[TestFixture]
public class AITWasmSectionsTests
{
    private static readonly byte[] Header = { 0x00, 0x61, 0x73, 0x6D, 0x01, 0x00, 0x00, 0x00 };

    private static void AppendU32(List<byte> dst, uint v)
    {
        do
        {
            byte b = (byte)(v & 0x7F);
            v >>= 7;
            if (v != 0) b |= 0x80;
            dst.Add(b);
        } while (v != 0);
    }

    private static void AppendSection(List<byte> dst, byte id, int payloadSize)
    {
        dst.Add(id);
        AppendU32(dst, (uint)payloadSize);
        for (int i = 0; i < payloadSize; i++) dst.Add((byte)(i & 0xFF));
    }

    private static byte[] Module(params (byte id, int size)[] sections)
    {
        var bytes = new List<byte>(Header);
        foreach (var (id, size) in sections) AppendSection(bytes, id, size);
        return bytes.ToArray();
    }

    [Test]
    public void TryParse_SumsCodeDataAndCustomSections_WithMultiByteLeb128()
    {
        // 300 과 70000 은 LEB128 2바이트/3바이트 크기다.
        byte[] wasm = Module((1, 5), (10, 70000), (11, 300), (0, 12), (0, 8));

        Assert.IsTrue(AITWasmSections.TryParse(wasm, out var s));
        Assert.AreEqual(70000, s.codeBytes);
        Assert.AreEqual(300, s.dataBytes);
        Assert.AreEqual(20, s.customBytes);
        Assert.AreEqual(5, s.sectionCount);
        Assert.AreEqual(wasm.Length, s.totalBytes);
    }

    [Test]
    public void TryParse_HeaderOnly_IsValidWithZeroSections()
    {
        Assert.IsTrue(AITWasmSections.TryParse(Header, out var s));
        Assert.AreEqual(0, s.sectionCount);
        Assert.AreEqual(0, s.codeBytes);
    }

    [Test]
    public void TryParse_RejectsBadMagicVersionAndTruncatedSections()
    {
        Assert.IsFalse(AITWasmSections.TryParse(null, out _));
        Assert.IsFalse(AITWasmSections.TryParse(new byte[4], out _));

        byte[] badMagic = (byte[])Header.Clone();
        badMagic[1] = 0x00;
        Assert.IsFalse(AITWasmSections.TryParse(badMagic, out _));

        byte[] badVersion = (byte[])Header.Clone();
        badVersion[4] = 0x02;
        Assert.IsFalse(AITWasmSections.TryParse(badVersion, out _));

        // 섹션 길이가 파일 끝을 넘는다.
        byte[] ok = Module((10, 100));
        byte[] truncated = new byte[ok.Length - 10];
        System.Array.Copy(ok, truncated, truncated.Length);
        Assert.IsFalse(AITWasmSections.TryParse(truncated, out _));

        // size LEB128 이 5바이트를 넘는다.
        var bad = new List<byte>(Header) { 10, 0x80, 0x80, 0x80, 0x80, 0x80, 0x01 };
        Assert.IsFalse(AITWasmSections.TryParse(bad.ToArray(), out _));
    }

    [Test]
    public void TryReadU32_ReadsKnownValues()
    {
        int pos = 0;
        Assert.IsTrue(AITWasmSections.TryReadU32(new byte[] { 0xE5, 0x8E, 0x26 }, ref pos, out uint v)); // 624485
        Assert.AreEqual(624485u, v);
        Assert.AreEqual(3, pos);
    }

    [Test]
    public void TryMeasureFile_PlainWasm_And_Missing()
    {
        string dir = Path.Combine(Path.GetTempPath(), "ait-wasm-sections-test-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string path = Path.Combine(dir, "t.wasm");
            File.WriteAllBytes(path, Module((10, 1234), (11, 56)));
            Assert.IsTrue(AITWasmSections.TryMeasureFile(path, out var s, out string err), err);
            Assert.AreEqual(1234, s.codeBytes);
            Assert.AreEqual(56, s.dataBytes);

            Assert.IsFalse(AITWasmSections.TryMeasureFile(Path.Combine(dir, "none.wasm"), out _, out string missing));
            Assert.IsNotEmpty(missing);

            File.WriteAllBytes(Path.Combine(dir, "bad.wasm"), new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 });
            Assert.IsFalse(AITWasmSections.TryMeasureFile(Path.Combine(dir, "bad.wasm"), out _, out string bad));
            Assert.IsNotEmpty(bad);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public void TryMeasureFile_GzipWasm_DecompressesThroughNode()
    {
        if (!AITBrotliCompressor.TryResolveNode(out _))
        {
            Assert.Ignore("내장 Node 를 쓸 수 없는 환경 — 압축 wasm 해제 경로는 건너뜀");
        }

        string dir = Path.Combine(Path.GetTempPath(), "ait-wasm-sections-test-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string gz = Path.Combine(dir, "t.wasm.gz");
            using (var fs = File.Create(gz))
            using (var z = new GZipStream(fs, CompressionMode.Compress))
            {
                byte[] raw = Module((10, 4096), (11, 17));
                z.Write(raw, 0, raw.Length);
            }

            Assert.IsTrue(AITWasmSections.TryMeasureFile(gz, out var s, out string err), err);
            Assert.AreEqual(4096, s.codeBytes);
            Assert.AreEqual(17, s.dataBytes);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
