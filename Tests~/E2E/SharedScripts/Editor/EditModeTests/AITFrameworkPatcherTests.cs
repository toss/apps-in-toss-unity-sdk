// -----------------------------------------------------------------------
// AITFrameworkPatcherTests.cs - framework 패치(context-resume-offset) 검증
//
// 이 저장소는 공개라서 Unity framework 원본을 커밋하지 않는다. 합성 텍스트는 패처가 가진 앵커 스니펫만 이어 붙여 만든다.
//   · 텍스트: 앵커가 정확히 1회일 때만 치환, 0회/2회면 건너뛰고 원문 유지, 이미 패치된 텍스트는 그대로(멱등)
//   · 파일(Apply): 무압축/.gz 합성 framework 를 실제로 풀어 패치하고 다시 쓴다. .unityweb 이 있으면 아무것도 건드리지 않는다.
//     앵커가 없는 파일은 바이트 그대로 둔다. (.br 는 내장 Node 가 필요해 여기서 다루지 않는다)
// -----------------------------------------------------------------------

using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using NUnit.Framework;
using AppsInToss.Editor;

[TestFixture]
[Category("Unit")]
public class AITFrameworkPatcherTests
{
    private string _tempDir;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ait-fwpatcher-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            if (_tempDir != null && Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }
        catch { /* best-effort */ }
    }

    // 앵커의 열린 괄호를 닫아 주는 최소 골격. 앞뒤 문장은 Unity 소스가 아니다.
    private static string Synthetic(string anchor)
    {
        return "var pending=[];var audioInstance={source:{}};"
            + "pending.push({channel:audioInstance," + anchor + ";"
            + "var tail=1;";
    }

    private static string WithAnchor() => Synthetic(AITFrameworkPatcher.ResumeOffsetFrom);

    private static byte[] Gzip(string text)
    {
        using (var ms = new MemoryStream())
        {
            using (var gz = new GZipStream(ms, CompressionLevel.Optimal, true))
            {
                byte[] bytes = new UTF8Encoding(false).GetBytes(text);
                gz.Write(bytes, 0, bytes.Length);
            }
            return ms.ToArray();
        }
    }

    private static string Gunzip(byte[] data)
    {
        using (var ms = new MemoryStream(data))
        using (var gz = new GZipStream(ms, CompressionMode.Decompress))
        using (var reader = new StreamReader(gz, new UTF8Encoding(false)))
        {
            return reader.ReadToEnd();
        }
    }

    // ─────────────────────────── 텍스트 ───────────────────────────

    [Test]
    public void PatchText_ReplacesOffsetWithZero()
    {
        string src = WithAnchor();

        var r = AITFrameworkPatcher.PatchText(src);

        CollectionAssert.AreEqual(new[] { AITFrameworkPatcher.GroupResumeOffset }, r.Applied);
        Assert.IsEmpty(r.Skipped);
        StringAssert.Contains("pending.push({channel:audioInstance,offset:0})", r.Source, "context-resume-offset 치환이 들어가야 한다");
        StringAssert.DoesNotContain("playbackPausedAtPosition", r.Source);
        StringAssert.EndsWith(";var tail=1;", r.Source, "앵커 밖의 텍스트는 그대로여야 한다");
    }

    [Test]
    public void PatchText_NoAnchor_IsSkippedAndUnchanged()
    {
        string src = "var a=1;pending.push({channel:audioInstance,offset:7});";

        var r = AITFrameworkPatcher.PatchText(src);

        Assert.IsEmpty(r.Applied);
        Assert.AreEqual(1, r.Skipped.Count);
        StringAssert.Contains("0회", r.Skipped[0]);
        Assert.AreEqual(src, r.Source);
    }

    [Test]
    public void PatchText_AnchorTwice_IsSkippedAndUnchanged()
    {
        string src = WithAnchor() + WithAnchor();

        var r = AITFrameworkPatcher.PatchText(src);

        Assert.IsEmpty(r.Applied);
        Assert.AreEqual(1, r.Skipped.Count);
        StringAssert.Contains("2회", r.Skipped[0]);
        Assert.AreEqual(src, r.Source);
    }

    [Test]
    public void PatchText_IsIdempotent()
    {
        var first = AITFrameworkPatcher.PatchText(WithAnchor());

        var second = AITFrameworkPatcher.PatchText(first.Source);

        Assert.IsEmpty(second.Applied, "이미 패치된 텍스트에는 앵커가 없으므로 다시 적용되지 않는다");
        Assert.AreEqual(first.Source, second.Source);
    }

    [Test]
    public void PatchText_EmptyInput_IsSkipped()
    {
        var r = AITFrameworkPatcher.PatchText(string.Empty);

        Assert.IsEmpty(r.Applied);
        Assert.AreEqual(1, r.Skipped.Count);
    }

    [Test]
    public void PatchText_DollarInSource_IsKept()
    {
        string src = "var x='$&$1';" + WithAnchor();

        var r = AITFrameworkPatcher.PatchText(src);

        StringAssert.StartsWith("var x='$&$1';", r.Source);
        StringAssert.Contains("offset:0})", r.Source);
    }

    [Test]
    public void GetFrameworkKind_RecognizesCompressionSuffixes()
    {
        Assert.AreEqual("br", AITFrameworkPatcher.GetFrameworkKind("web.framework.js.br"));
        Assert.AreEqual("gz", AITFrameworkPatcher.GetFrameworkKind("web.framework.js.gz"));
        Assert.AreEqual("none", AITFrameworkPatcher.GetFrameworkKind("web.framework.js"));
        Assert.IsNull(AITFrameworkPatcher.GetFrameworkKind("web.wasm"));
        Assert.IsNull(AITFrameworkPatcher.GetFrameworkKind("web.framework.js.unityweb"));
        Assert.IsNull(AITFrameworkPatcher.GetFrameworkKind(null));
    }

    // ─────────────────────────── 파일 ───────────────────────────

    [Test]
    public void Apply_PlainFramework_IsPatchedInPlace_AndSecondRunDoesNothing()
    {
        string path = Path.Combine(_tempDir, "web.framework.js");
        File.WriteAllText(path, WithAnchor(), new UTF8Encoding(false));

        Assert.AreEqual(1, AITFrameworkPatcher.Apply(_tempDir));

        string after = File.ReadAllText(path, new UTF8Encoding(false));
        StringAssert.Contains("pending.push({channel:audioInstance,offset:0})", after);
        Assert.AreEqual(0, AITFrameworkPatcher.Apply(_tempDir), "이미 패치된 파일은 다시 패치하지 않는다");
        Assert.AreEqual(after, File.ReadAllText(path, new UTF8Encoding(false)));
        Assert.IsFalse(File.Exists(path + ".aitpatch.tmp"), "임시 파일이 남으면 안 된다");
    }

    [Test]
    public void Apply_GzipFramework_IsDecodedPatchedAndRecompressed()
    {
        string path = Path.Combine(_tempDir, "web.framework.js.gz");
        File.WriteAllBytes(path, Gzip(WithAnchor()));

        Assert.AreEqual(1, AITFrameworkPatcher.Apply(_tempDir));

        string after = Gunzip(File.ReadAllBytes(path));
        StringAssert.Contains("pending.push({channel:audioInstance,offset:0})", after);
        StringAssert.DoesNotContain("playbackPausedAtPosition", after);
    }

    [Test]
    public void Apply_NoAnchor_LeavesFileBytesUntouched()
    {
        string path = Path.Combine(_tempDir, "web.framework.js");
        byte[] original = new UTF8Encoding(false).GetBytes("var a=1;");
        File.WriteAllBytes(path, original);

        Assert.AreEqual(0, AITFrameworkPatcher.Apply(_tempDir));

        CollectionAssert.AreEqual(original, File.ReadAllBytes(path));
    }

    [Test]
    public void Apply_UnitywebPresent_TouchesNothing()
    {
        string fw = Path.Combine(_tempDir, "web.framework.js");
        File.WriteAllText(fw, WithAnchor(), new UTF8Encoding(false));
        File.WriteAllBytes(Path.Combine(_tempDir, "web.data.unityweb"), new byte[] { 1, 2, 3 });
        string before = File.ReadAllText(fw, new UTF8Encoding(false));

        Assert.AreEqual(0, AITFrameworkPatcher.Apply(_tempDir));

        Assert.AreEqual(before, File.ReadAllText(fw, new UTF8Encoding(false)));
    }

    [Test]
    public void Apply_MissingOrNullDirectory_ReturnsZeroWithoutThrowing()
    {
        Assert.AreEqual(0, AITFrameworkPatcher.Apply(null));
        Assert.AreEqual(0, AITFrameworkPatcher.Apply(Path.Combine(_tempDir, "nope")));
    }
}
