// -----------------------------------------------------------------------
// AITFrameworkPatcherTests.cs - framework 오디오 패치 엔진(AITFrameworkPatcher) 검증
//
// 이 저장소는 공개라서 Unity framework 원본을 커밋하지 않는다. 합성 텍스트는 패처가 가진 앵커 스니펫
// (AITFrameworkPatcher.BuildGroups 가 돌려주는 값)을 이어 붙여 만든다.
//   · 그룹 적용: 앵커가 정확히 1회일 때만 적용, prologue 는 마지막
//   · 건너뛰기: 앵커 0회/2회, 그룹 안 앵커 하나라도 어긋나면 그룹 통째로, 선행 그룹 미적용 시 prologue
//   · 멱등: 이미 패치된 텍스트는 그대로
//   · literal 굽기(force/minSec), '$' 안전, payload 정리
//   · 파일 파이프라인(Apply): .unityweb/이미 패치된 이름/설정 0 은 건드리지 않고,
//     Node 가 있으면 실제 .br/.gz/무압축 합성 framework 를 풀어 패치→검증→재압축→.aitpN rename 까지 확인
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using AppsInToss;
using AppsInToss.Editor;

[TestFixture]
[Category("Unit")]
public class AITFrameworkPatcherTests
{
    private const string FakePayload = "function aitAudioProbe(u){return null}\nWEBAudio.aitDecide=function(){return null};\nWEBAudio.aitLog=function(){};\n";

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

    // ─────────────────────────── 합성 framework ───────────────────────────

    private static List<AITFrameworkPatcher.PatchGroup> Groups()
    {
        return AITFrameworkPatcher.BuildGroups(false, 10f, FakePayload);
    }

    private static string Anchor(string group, int index)
    {
        return Groups().First(g => g.Name == group).Edits[index].Anchor;
    }

    /// <summary>
    /// 모든 그룹의 앵커를 문법적으로 유효한 JS 골격에 끼워 넣은 합성 framework.
    /// (Unity 소스를 복사하지 않고 패처의 앵커만으로 만든다. 골격은 앵커의 열린 괄호를 닫아 주는 최소한이다.)
    /// </summary>
    private static string SyntheticFramework(Func<string, int, string, string> mutate = null)
    {
        string A(string g, int i)
        {
            string a = Anchor(g, i);
            return mutate != null ? mutate(g, i, a) : a;
        }

        return "var WEBAudio={audioInstances:{},audioInstanceIdCounter:0,audioWebEnabled:1};var HEAPU8=new Uint8Array(8);"
            + "function jsAudioGetMimeTypeFromType(fmodSoundType){switch(fmodSoundType){" + A(AITFrameworkPatcher.GroupMimeMap, 0) + "}}"
            + A(AITFrameworkPatcher.GroupClipMeta, 0)
            + A(AITFrameworkPatcher.GroupClipMeta, 1)
            + "soundClip.createSourceNode=function(){var mediaElement=new Audio;"
            + A(AITFrameworkPatcher.GroupPitchPosition, 0)
            + A(AITFrameworkPatcher.GroupPitchPosition, 1)    // 끝이 "function _JS_Sound_Load(" — prologue 앵커의 앞부분과 겹친다
            + "ptr,length,decompress,fmodSoundType){return 0}"
            + "var holder={};holder.release=function(){" + A(AITFrameworkPatcher.GroupSourceRelease, 0) + "var tail=1;";
    }

    private static int Count(string text, string anchor) => AITFrameworkPatcher.CountOccurrences(text, anchor);

    // ─────────────────────────── 그룹 적용 ───────────────────────────

    [Test]
    public void PatchText_AppliesAllGroups_PrologueLast()
    {
        string src = SyntheticFramework();

        var r = AITFrameworkPatcher.PatchText(src, false, 10f, FakePayload);

        CollectionAssert.AreEqual(
            new[]
            {
                AITFrameworkPatcher.GroupMimeMap, AITFrameworkPatcher.GroupClipMeta, AITFrameworkPatcher.GroupPitchPosition,
                AITFrameworkPatcher.GroupSourceRelease, AITFrameworkPatcher.GroupPrologue,
            },
            r.Applied, "적용 순서에서 prologue 가 마지막이어야 한다");
        Assert.IsEmpty(r.Skipped);
        Assert.IsFalse(r.AlreadyPatched);
        Assert.AreNotEqual(src, r.Source);
        StringAssert.Contains("aitInfo", r.Source, "compressed-clip-meta 치환이 들어가야 한다");
        StringAssert.Contains("estimatePlaybackPosition", r.Source, "media-source-pitch-position 치환이 들어가야 한다");
        StringAssert.Contains("aitSrc", r.Source, "media-source-release 치환이 들어가야 한다");
        StringAssert.Contains("audio/ogg", r.Source, "mime-map 치환이 들어가야 한다");
        StringAssert.Contains("WEBAudio.aitDecide(ptr,length,decompress)", r.Source, "prologue 호출이 들어가야 한다");
    }

    [Test]
    public void PatchText_HelperBlockPrecedesLoadFunction_AndEndsWithNewline()
    {
        var r = AITFrameworkPatcher.PatchText(SyntheticFramework(), false, 10f, FakePayload);

        int marker = r.Source.IndexOf(AITFrameworkPatcher.MarkerPrefix, StringComparison.Ordinal);
        int cfg = r.Source.IndexOf("WEBAudio.aitCfg=", StringComparison.Ordinal);
        int load = r.Source.IndexOf("function _JS_Sound_Load(ptr", StringComparison.Ordinal);
        Assert.Greater(marker, 0);
        Assert.Greater(cfg, marker);
        Assert.Greater(load, cfg, "설정 literal 은 _JS_Sound_Load 앞에 있어야 한다");
        Assert.AreEqual('\n', r.Source[load - 1], "helper 블록은 줄바꿈으로 끝나 압축된 한 줄 안의 주석이 뒤 코드를 삼키지 않아야 한다");
        Assert.AreEqual(1, Count(r.Source, AITFrameworkPatcher.MarkerPrefix));
    }

    [Test]
    public void PatchText_BakesForceAndMinSecondsLiterals()
    {
        var off = AITFrameworkPatcher.PatchText(SyntheticFramework(), false, 10f, FakePayload);
        var on = AITFrameworkPatcher.PatchText(SyntheticFramework(), true, 12.5f, FakePayload);

        StringAssert.Contains("WEBAudio.aitCfg={force:0,minSec:10};", off.Source);
        StringAssert.Contains("WEBAudio.aitCfg={force:1,minSec:12.5};", on.Source);
    }

    [Test]
    public void PatchText_KeepsDollarSequencesInPayloadLiteral()
    {
        // String.Replace 의 '$' 패턴 해석 같은 사고가 없어야 한다(치환은 이어붙이기).
        const string payload = "function aitAudioProbe(u){return '$&$1$$'}\nWEBAudio.aitDecide=function(){};WEBAudio.aitLog=function(){};\n";
        var r = AITFrameworkPatcher.PatchText(SyntheticFramework(), false, 10f, payload);

        StringAssert.Contains("'$&$1$$'", r.Source);
    }

    // ─────────────────────────── 앵커 개수 != 1 → 그룹 건너뛰기 ───────────────────────────

    [Test]
    public void PatchText_MissingAnchor_SkipsOnlyThatGroup()
    {
        string src = SyntheticFramework((g, i, a) => g == AITFrameworkPatcher.GroupSourceRelease ? "/*gone*/" : a);

        var r = AITFrameworkPatcher.PatchText(src, false, 10f, FakePayload);

        CollectionAssert.DoesNotContain(r.Applied, AITFrameworkPatcher.GroupSourceRelease);
        Assert.IsTrue(r.Skipped.Any(s => s.StartsWith(AITFrameworkPatcher.GroupSourceRelease + ":") && s.Contains("0회")), string.Join("|", r.Skipped));
        CollectionAssert.Contains(r.Applied, AITFrameworkPatcher.GroupClipMeta);
        CollectionAssert.Contains(r.Applied, AITFrameworkPatcher.GroupPrologue, "release 는 prologue 의 선행 그룹이 아니다");
        StringAssert.DoesNotContain("aitSrc", r.Source);
    }

    [Test]
    public void PatchText_DuplicateAnchor_SkipsGroup_AndPrologueDependsOnIt()
    {
        string dup = Anchor(AITFrameworkPatcher.GroupClipMeta, 1);
        string src = SyntheticFramework() + dup; // 같은 앵커가 2번

        var r = AITFrameworkPatcher.PatchText(src, false, 10f, FakePayload);

        CollectionAssert.DoesNotContain(r.Applied, AITFrameworkPatcher.GroupClipMeta);
        Assert.IsTrue(r.Skipped.Any(s => s.StartsWith(AITFrameworkPatcher.GroupClipMeta + ":") && s.Contains("2회")), string.Join("|", r.Skipped));
        CollectionAssert.DoesNotContain(r.Applied, AITFrameworkPatcher.GroupPrologue, "선행 그룹이 없으면 prologue 도 건너뛴다");
        Assert.IsTrue(r.Skipped.Any(s => s.StartsWith(AITFrameworkPatcher.GroupPrologue + ":") && s.Contains("선행")), string.Join("|", r.Skipped));
        CollectionAssert.Contains(r.Applied, AITFrameworkPatcher.GroupMimeMap);
        CollectionAssert.Contains(r.Applied, AITFrameworkPatcher.GroupPitchPosition);
    }

    [Test]
    public void PatchText_GroupIsAtomic_NoPartialEditWhenSecondAnchorMissing()
    {
        // clip-meta 의 두 번째 앵커만 없애면, 첫 번째 앵커가 있어도 그 그룹의 어떤 편집도 들어가지 않아야 한다.
        string src = SyntheticFramework((g, i, a) => g == AITFrameworkPatcher.GroupClipMeta && i == 1 ? "/*gone*/" : a);

        var r = AITFrameworkPatcher.PatchText(src, false, 10f, FakePayload);

        CollectionAssert.DoesNotContain(r.Applied, AITFrameworkPatcher.GroupClipMeta);
        StringAssert.DoesNotContain("aitInfo", r.Source, "부분 적용이 남으면 안 된다");
        Assert.AreEqual(1, Count(r.Source, Anchor(AITFrameworkPatcher.GroupClipMeta, 0)), "첫 앵커는 원문 그대로여야 한다");
    }

    [Test]
    public void PatchText_PrologueAnchorMissing_SkipsOnlyPrologue()
    {
        // pitch-position 의 두 번째 앵커 끝("function _JS_Sound_Load(")만 남기고 인자 목록을 바꾸면 prologue 앵커가 사라진다.
        string src = SyntheticFramework().Replace("ptr,length,decompress,fmodSoundType){return 0}", "a,b){return 0}");

        var r = AITFrameworkPatcher.PatchText(src, false, 10f, FakePayload);

        CollectionAssert.DoesNotContain(r.Applied, AITFrameworkPatcher.GroupPrologue);
        Assert.IsTrue(r.Skipped.Any(s => s.StartsWith(AITFrameworkPatcher.GroupPrologue + ":")), string.Join("|", r.Skipped));
        CollectionAssert.Contains(r.Applied, AITFrameworkPatcher.GroupClipMeta);
        StringAssert.DoesNotContain("aitCfg", r.Source, "prologue 가 없으면 helper 도 들어가지 않는다");
    }

    [Test]
    public void PatchText_NoAnchors_ReturnsInputUntouched()
    {
        const string stock = "var a=1;function foo(){return 2}";

        var r = AITFrameworkPatcher.PatchText(stock, true, 10f, FakePayload);

        Assert.IsEmpty(r.Applied);
        Assert.AreEqual(5, r.Skipped.Count, "그룹 5개 모두 사유와 함께 건너뛴다");
        Assert.AreSame(stock, r.Source);
    }

    [Test]
    public void PatchText_EmptyInput_IsSafe()
    {
        var r = AITFrameworkPatcher.PatchText(string.Empty, true, 10f, FakePayload);
        Assert.IsEmpty(r.Applied);
        Assert.AreEqual(string.Empty, r.Source);

        r = AITFrameworkPatcher.PatchText(null, true, 10f, FakePayload);
        Assert.IsEmpty(r.Applied);
    }

    // ─────────────────────────── 멱등 ───────────────────────────

    [Test]
    public void PatchText_IsIdempotent()
    {
        var first = AITFrameworkPatcher.PatchText(SyntheticFramework(), true, 10f, FakePayload);
        Assert.IsNotEmpty(first.Applied);

        var second = AITFrameworkPatcher.PatchText(first.Source, true, 10f, FakePayload);

        Assert.IsTrue(second.AlreadyPatched);
        Assert.IsEmpty(second.Applied);
        Assert.AreEqual(first.Source, second.Source, "두 번째 패치는 바이트가 같아야 한다");
    }

    // ─────────────────────────── 보조 함수 ───────────────────────────

    [TestCase(10f, "10")]
    [TestCase(12.5f, "12.5")]
    [TestCase(0f, "10")]
    [TestCase(-3f, "10")]
    [TestCase(float.NaN, "10")]
    [TestCase(float.PositiveInfinity, "10")]
    [TestCase(0.0004f, "10")]
    public void FormatSeconds_ProducesValidJsNumber(float seconds, string expected)
    {
        Assert.AreEqual(expected, AITFrameworkPatcher.FormatSeconds(seconds));
    }

    [Test]
    public void FormatSeconds_IsCultureInvariant()
    {
        CultureInfo saved = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            Assert.AreEqual("12.5", AITFrameworkPatcher.FormatSeconds(12.5f), "소수점은 항상 '.' 이어야 JS literal 이 된다");
        }
        finally
        {
            CultureInfo.CurrentCulture = saved;
        }
    }

    [Test]
    public void CountOccurrences_CountsNonOverlappingOrdinal()
    {
        Assert.AreEqual(0, AITFrameworkPatcher.CountOccurrences("abc", "x"));
        Assert.AreEqual(2, AITFrameworkPatcher.CountOccurrences("abcabc", "abc"));
        Assert.AreEqual(2, AITFrameworkPatcher.CountOccurrences("aaaa", "aa"), "겹치는 일치는 세지 않는다");
        Assert.AreEqual(0, AITFrameworkPatcher.CountOccurrences("abc", string.Empty));
        Assert.AreEqual(0, AITFrameworkPatcher.CountOccurrences(null, "a"));
        Assert.AreEqual(0, AITFrameworkPatcher.CountOccurrences("ABC", "abc"), "대소문자를 구분한다");
    }

    [TestCase("a.framework.js.br", "br")]
    [TestCase("a.framework.js.gz", "gz")]
    [TestCase("a.framework.js", "none")]
    [TestCase("a.FRAMEWORK.JS.BR", "br")]
    [TestCase("a.framework.js.unityweb", null)]
    [TestCase("a.loader.js", null)]
    [TestCase("a.data.br", null)]
    [TestCase("", null)]
    public void GetFrameworkKind_ClassifiesByExtension(string name, string expected)
    {
        Assert.AreEqual(expected, AITFrameworkPatcher.GetFrameworkKind(name));
    }

    [Test]
    public void PreparePayload_StripsFullLineCommentsAndModuleExport_AndNormalizesNewlines()
    {
        string raw = "// header\r\nfunction f(){ // trailing stays\r\n  return 1}\r\n  // indented comment\r\nif(typeof module!==\"undefined\")module.exports=f;\r\n";

        string p = AITFrameworkPatcher.PreparePayload(raw);

        StringAssert.DoesNotContain("\r", p);
        StringAssert.DoesNotContain("// header", p);
        StringAssert.DoesNotContain("indented comment", p);
        StringAssert.DoesNotContain("module.exports", p);
        StringAssert.Contains("function f(){ // trailing stays\n  return 1}\n", p, "코드 뒤 주석은 줄바꿈이 있으므로 그대로 둔다");
        Assert.IsTrue(p.EndsWith("\n"));
    }

    [Test]
    public void RuntimePayloadFile_IsAsciiAndHasContract()
    {
        string path = AITFrameworkPatcher.GetRuntimePayloadPathForTests();
        Assert.IsTrue(!string.IsNullOrEmpty(path) && File.Exists(path), "payload 파일을 찾지 못함: " + path);

        string text = File.ReadAllText(path);
        foreach (char c in text)
        {
            Assert.Less(c, 128, "payload 는 ASCII 만 써야 한다(framework 가 charset 없이 서빙돼도 안전)");
        }

        string p = AITFrameworkPatcher.PreparePayload(text);
        StringAssert.Contains("function aitAudioProbe(", p);
        StringAssert.Contains("WEBAudio.aitDecide=function", p);
        StringAssert.Contains("WEBAudio.aitLog=function", p);
        StringAssert.DoesNotContain("module.exports", p);
        foreach (string line in p.Split('\n'))
        {
            Assert.IsFalse(line.TrimStart().StartsWith("//"), "전체 줄 주석이 남아 있다: " + line);
        }
    }

    // ─────────────────────────── 설정 해석 ───────────────────────────

    [TestCase(-1, true, false)]   // 자동: 정확성 수정만, 강제 압축 재생은 꺼짐
    [TestCase(1, true, true)]     // 명시 활성: 강제 압축 재생
    [TestCase(0, false, false)]   // 명시 비활성: framework stock
    public void ResolveSettings_TriState(int setting, bool expectEnabled, bool expectForce)
    {
        var config = ScriptableObject.CreateInstance<AITEditorScriptObject>();
        try
        {
            config.audioForceCompressedPlayback = setting;
            AITFrameworkPatcher.ResolveSettings(config, out bool enabled, out bool force, out float minSec);
            Assert.AreEqual(expectEnabled, enabled);
            Assert.AreEqual(expectForce, force);
            Assert.AreEqual(10f, minSec, 0.0001f);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(config);
        }
    }

    [TestCase(20f, 20f)]
    [TestCase(0f, 10f)]
    [TestCase(-1f, 10f)]
    [TestCase(float.NaN, 10f)]
    public void ResolveSettings_MinSecondsFallsBackToTen(float stored, float expected)
    {
        var config = ScriptableObject.CreateInstance<AITEditorScriptObject>();
        try
        {
            config.audioForceCompressedMinSeconds = stored;
            AITFrameworkPatcher.ResolveSettings(config, out _, out _, out float minSec);
            Assert.AreEqual(expected, minSec, 0.0001f);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(config);
        }
    }

    [Test]
    public void ResolveSettings_NullConfig_BehavesLikeAuto()
    {
        AITFrameworkPatcher.ResolveSettings(null, out bool enabled, out bool force, out float minSec);
        Assert.IsTrue(enabled);
        Assert.IsFalse(force);
        Assert.AreEqual(10f, minSec, 0.0001f);
    }

    // ─────────────────────────── 파일 파이프라인: Node 불필요한 건너뛰기 경로 ───────────────────────────

    [Test]
    public void Apply_UnitywebPresent_TouchesNothing()
    {
        string br = Path.Combine(_tempDir, "h.framework.js.br");
        string uw = Path.Combine(_tempDir, "h.data.unityweb");
        File.WriteAllBytes(br, new byte[] { 1, 2, 3 });
        File.WriteAllBytes(uw, new byte[] { 4, 5, 6 });
        var renames = new Dictionary<string, string>();

        int n = AITFrameworkPatcher.Apply(_tempDir, NewConfig(1), renames);

        Assert.AreEqual(0, n);
        Assert.IsEmpty(renames);
        Assert.IsTrue(File.Exists(br));
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, File.ReadAllBytes(br));
    }

    [Test]
    public void Apply_AudioForceZero_TouchesNothing()
    {
        string js = Path.Combine(_tempDir, "h.framework.js");
        File.WriteAllText(js, "var a=1;");
        var renames = new Dictionary<string, string>();

        int n = AITFrameworkPatcher.Apply(_tempDir, NewConfig(0), renames);

        Assert.AreEqual(0, n);
        Assert.IsEmpty(renames);
        Assert.AreEqual("var a=1;", File.ReadAllText(js));
    }

    [Test]
    public void Apply_AlreadyPatchedName_IsSkipped()
    {
        string js = Path.Combine(_tempDir, "h.aitp1.framework.js");
        File.WriteAllText(js, "var a=1;");
        var renames = new Dictionary<string, string>();

        int n = AITFrameworkPatcher.Apply(_tempDir, NewConfig(1), renames);

        Assert.AreEqual(0, n);
        Assert.IsEmpty(renames);
        Assert.IsTrue(File.Exists(js));
    }

    [Test]
    public void Apply_MissingOrEmptyDirectory_IsSafe()
    {
        Assert.AreEqual(0, AITFrameworkPatcher.Apply(null, NewConfig(1), null));
        Assert.AreEqual(0, AITFrameworkPatcher.Apply(Path.Combine(_tempDir, "nope"), NewConfig(1), null));
        Assert.AreEqual(0, AITFrameworkPatcher.Apply(_tempDir, NewConfig(1), null));
    }

    // ─────────────────────────── 파일 파이프라인: 내장 Node 필요 ───────────────────────────

    [TestCase("none")]
    [TestCase("gz")]
    [TestCase("br")]
    public void Apply_PatchesRenamesAndRecompresses(string kind)
    {
        RequireNode(out string node);
        string source = SyntheticFramework();
        string stem = "abc123.framework.js";
        string name = WriteFramework(node, stem, kind, source);
        var renames = new Dictionary<string, string>();

        int n = AITFrameworkPatcher.Apply(_tempDir, NewConfig(1), renames);

        Assert.AreEqual(1, n);
        string expectedName = renames[name];
        StringAssert.IsMatch(
            @"^abc123\.aitp1-[0-9a-f]{8}\.framework\.js" + (kind == "none" ? string.Empty : "\\." + kind) + "$",
            expectedName);
        Assert.IsTrue(AITPatchedFileNaming.TryParseConfigHash(expectedName, out _), "설정 해시가 이름에 들어간다");
        Assert.IsTrue(AITFrameworkPatcher.LastApplyClipMetaApplied, "clip-meta 그룹이 적용됐음을 기록한다");
        Assert.IsFalse(File.Exists(Path.Combine(_tempDir, name)), "원본 이름은 남지 않아야 한다");
        Assert.IsTrue(File.Exists(Path.Combine(_tempDir, expectedName)));
        Assert.AreEqual(expectedName, renames[name]);

        string patched = ReadFramework(node, Path.Combine(_tempDir, expectedName), kind);
        StringAssert.Contains("WEBAudio.aitCfg={force:1,minSec:10};", patched);
        StringAssert.Contains(AITFrameworkPatcher.MarkerPrefix, patched);
        StringAssert.Contains("estimatePlaybackPosition", patched);
    }

    [Test]
    public void Apply_AutoSetting_BakesForceZero()
    {
        RequireNode(out string node);
        string name = WriteFramework(node, "x.framework.js", "none", SyntheticFramework());

        var renames = new Dictionary<string, string>();
        Assert.AreEqual(1, AITFrameworkPatcher.Apply(_tempDir, NewConfig(-1), renames));
        string patched = File.ReadAllText(Path.Combine(_tempDir, renames[name]));
        StringAssert.Contains("WEBAudio.aitCfg={force:0,minSec:10};", patched, "자동(-1)은 정확성 수정만 적용하고 강제 압축 재생은 꺼 둔다");
    }

    [Test]
    public void Apply_ConfigChange_ChangesPatchedName()
    {
        RequireNode(out string node);
        string source = SyntheticFramework();
        string name = WriteFramework(node, "c.framework.js", "none", source);
        var forced = new Dictionary<string, string>();
        Assert.AreEqual(1, AITFrameworkPatcher.Apply(_tempDir, NewConfig(1), forced));

        File.Delete(Path.Combine(_tempDir, forced[name]));
        WriteFramework(node, "c.framework.js", "none", source);
        var auto = new Dictionary<string, string>();
        Assert.AreEqual(1, AITFrameworkPatcher.Apply(_tempDir, NewConfig(-1), auto));

        Assert.AreNotEqual(forced[name], auto[name], "강제 압축 여부가 다르면 패치 바이트가 달라지므로 이름도 달라야 한다");
    }

    [Test]
    public void Apply_NothingApplied_ReportsClipMetaNotApplied()
    {
        File.WriteAllText(Path.Combine(_tempDir, "n.framework.js"), "var stock=1;");
        AITFrameworkPatcher.Apply(_tempDir, NewConfig(0), null);
        Assert.IsFalse(AITFrameworkPatcher.LastApplyClipMetaApplied);
    }

    [Test]
    public void Apply_SyntaxBreakingResult_IsRejected_KeepsOriginal()
    {
        RequireNode(out string node);
        // 앵커는 모두 맞지만 골격이 문법적으로 깨져 있다(앵커 조각만 이어 붙인 텍스트).
        string broken = string.Join(";", Groups().SelectMany(g => g.Edits).Select(e => e.Anchor));
        string name = WriteFramework(node, "b.framework.js", "none", broken);
        var renames = new Dictionary<string, string>();

        int n = AITFrameworkPatcher.Apply(_tempDir, NewConfig(1), renames);

        Assert.AreEqual(0, n, "node --check 를 통과하지 못하면 채택하지 않는다");
        Assert.IsEmpty(renames);
        Assert.IsTrue(File.Exists(Path.Combine(_tempDir, name)));
        Assert.AreEqual(broken, File.ReadAllText(Path.Combine(_tempDir, name)));
    }

    [Test]
    public void Apply_NoAnchors_LeavesFileAndNameUnchanged()
    {
        RequireNode(out string node);
        string name = WriteFramework(node, "s.framework.js", "br", "var stock=1;function f(){return 1}");
        byte[] before = File.ReadAllBytes(Path.Combine(_tempDir, name));
        var renames = new Dictionary<string, string>();

        int n = AITFrameworkPatcher.Apply(_tempDir, NewConfig(1), renames);

        Assert.AreEqual(0, n);
        Assert.IsEmpty(renames);
        CollectionAssert.AreEqual(before, File.ReadAllBytes(Path.Combine(_tempDir, name)), "패치할 게 없으면 재압축하지 않는다");
    }

    [Test]
    public void Apply_SecondRun_DoesNotPatchAgain()
    {
        RequireNode(out string node);
        WriteFramework(node, "r.framework.js", "none", SyntheticFramework());
        var renames = new Dictionary<string, string>();
        Assert.AreEqual(1, AITFrameworkPatcher.Apply(_tempDir, NewConfig(1), renames));
        string patchedName = renames["r.framework.js"];
        string once = File.ReadAllText(Path.Combine(_tempDir, patchedName));

        var renames2 = new Dictionary<string, string>();
        int n = AITFrameworkPatcher.Apply(_tempDir, NewConfig(1), renames2);

        Assert.AreEqual(0, n, "이미 .aitpN 이름이면 건너뛴다");
        Assert.IsEmpty(renames2);
        Assert.AreEqual(once, File.ReadAllText(Path.Combine(_tempDir, patchedName)));
    }

    // ─────────────────────────── 헬퍼 ───────────────────────────

    private static AITEditorScriptObject NewConfig(int audioForce)
    {
        var config = ScriptableObject.CreateInstance<AITEditorScriptObject>();
        config.audioForceCompressedPlayback = audioForce;
        return config;
    }

    private static void RequireNode(out string node)
    {
        if (!AITBrotliCompressor.TryResolveNode(out node) || string.IsNullOrEmpty(node))
        {
            Assert.Ignore("내장 Node 미가용 — framework 패치 파이프라인 테스트 건너뜀");
        }
    }

    // 합성 framework 를 kind 에 맞게 압축해 _tempDir 에 쓰고 파일명을 돌려준다.
    private string WriteFramework(string node, string stem, string kind, string text)
    {
        string name = kind == "none" ? stem : stem + "." + kind;
        string dest = Path.Combine(_tempDir, name);
        string raw = Path.Combine(_tempDir, "raw-" + Guid.NewGuid().ToString("N").Substring(0, 6) + ".js");
        File.WriteAllText(raw, text, new UTF8Encoding(false));
        if (kind == "none")
        {
            File.Move(raw, dest);
            return name;
        }

        RunUtil(node, kind == "br" ? "enc-br" : "enc-gz", raw, dest);
        File.Delete(raw);
        return name;
    }

    private string ReadFramework(string node, string path, string kind)
    {
        if (kind == "none")
        {
            return File.ReadAllText(path, new UTF8Encoding(false));
        }

        string raw = Path.Combine(_tempDir, "dec-" + Guid.NewGuid().ToString("N").Substring(0, 6) + ".js");
        RunUtil(node, kind == "br" ? "dec-br" : "dec-gz", path, raw);
        return File.ReadAllText(raw, new UTF8Encoding(false));
    }

    private const string UtilJs =
        "const zlib=require('zlib'),fs=require('fs');\n" +
        "const [mode,src,dst]=process.argv.slice(2);\n" +
        "const b=fs.readFileSync(src);\n" +
        "let o;\n" +
        "if(mode==='enc-br')o=zlib.brotliCompressSync(b,{params:{[zlib.constants.BROTLI_PARAM_QUALITY]:5}});\n" +
        "else if(mode==='dec-br')o=zlib.brotliDecompressSync(b);\n" +
        "else if(mode==='enc-gz')o=zlib.gzipSync(b);\n" +
        "else if(mode==='dec-gz')o=zlib.gunzipSync(b);\n" +
        "else throw new Error('bad mode');\n" +
        "fs.writeFileSync(dst,o);\n";

    private void RunUtil(string node, string mode, string src, string dst)
    {
        string util = Path.Combine(_tempDir, "util.js");
        if (!File.Exists(util))
        {
            File.WriteAllText(util, UtilJs);
        }

        var psi = new ProcessStartInfo
        {
            FileName = node,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = _tempDir,
        };
        psi.ArgumentList.Add(util);
        psi.ArgumentList.Add(mode);
        psi.ArgumentList.Add(src);
        psi.ArgumentList.Add(dst);
        using (var p = Process.Start(psi))
        {
            string err = p.StandardError.ReadToEnd();
            p.StandardOutput.ReadToEnd();
            Assert.IsTrue(p.WaitForExit(60000), "node util 시간 초과");
            Assert.AreEqual(0, p.ExitCode, "node util 실패: " + err);
        }
    }
}
