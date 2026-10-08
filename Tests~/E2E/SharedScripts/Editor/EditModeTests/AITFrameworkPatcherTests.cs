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
//   · stacktrace-lazy: Error.js 머리말 모양의 합성 텍스트로 적용·멱등·건너뛰기·오디오 독립·괄호 스캔·tri-state·
//     (Node 가 있으면) 패치 결과를 실제로 실행해 지연 계산과 setter 확인
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
    private string _savedLazyEnv;

    [SetUp]
    public void SetUp()
    {
        // 환경 변수 오버라이드가 tri-state 기대값을 흔들지 않게 비워 두고 TearDown 에서 되돌린다.
        _savedLazyEnv = Environment.GetEnvironmentVariable(AITFrameworkPatcher.LazyStackTraceEnvVar);
        Environment.SetEnvironmentVariable(AITFrameworkPatcher.LazyStackTraceEnvVar, null);
        _tempDir = Path.Combine(Path.GetTempPath(), "ait-fwpatcher-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        Environment.SetEnvironmentVariable(AITFrameworkPatcher.LazyStackTraceEnvVar, _savedLazyEnv);
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

        // 오디오 0 + stacktrace-lazy 0: 둘 다 꺼야 framework 를 아예 건드리지 않는다(stock 대조군).
        int n = AITFrameworkPatcher.Apply(_tempDir, NewConfig(0, 0), renames);

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

    private static AITEditorScriptObject NewConfig(int audioForce, int lazyStack = -1)
    {
        var config = ScriptableObject.CreateInstance<AITEditorScriptObject>();
        config.audioForceCompressedPlayback = audioForce;
        config.frameworkLazyStackTraceMode = lazyStack;
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

    // ─────────────────────────── P0-6.3: 압축 클립 JS 중간 사본 없음 ───────────────────────────

    [Test]
    public void PatchText_Prologue_BuildsBlobFromHeapSubarray_NoIntermediateCopy()
    {
        var r = AITFrameworkPatcher.PatchText(SyntheticFramework(), false, 10f, FakePayload);

        int load = r.Source.IndexOf("function _JS_Sound_Load(ptr", StringComparison.Ordinal);
        Assert.Greater(load, 0);
        string prologue = r.Source.Substring(load);

        // 압축 클립은 힙의 subarray 뷰를 곧장 Blob 에 넘긴다(Blob 생성자가 복사한다). stock 의 HEAPU8.buffer.slice 같은 JS 쪽
        // 중간 ArrayBuffer 사본이 클립 수명 동안 남는 일이 없어야 한다.
        StringAssert.Contains("jsAudioCreateCompressedSoundClip(HEAPU8.subarray(ptr,ptr+length)", prologue);
        StringAssert.DoesNotContain(".slice(", prologue, "prologue 가 힙을 slice 로 복사하면 클립 크기만큼의 JS 사본이 상주한다");
        StringAssert.DoesNotContain("HEAPU8.buffer", prologue);
        // 압축 클립 객체가 원본 바이트를 붙들지 않는다: audioData 인자는 Blob 생성에만 쓰이고 soundClip 필드로 저장되지 않는다.
        StringAssert.DoesNotContain("soundClip.audioData", r.Source);
        StringAssert.DoesNotContain("audioData:", r.Source);
    }

    // ─────────────────────────── stacktrace-lazy ───────────────────────────

    // Error.js 머리말 모양의 합성 텍스트(Unity 소스를 복사하지 않고 구조만 흉내 낸다). 공백이 있는 형태와 없는 형태.
    private const string StackRef =
        @"var stackTraceReference=""(^|\\n)(\\s+at\\s+|)jsStackTrace(\\s+\\(|@)([^\\n]+):\\d+:\\d+(\\)|)(\\n|$)"";";

    private const string StackRhs =
        @"new RegExp(stackTraceReference.replace(""([^\\n]+)"",stackTraceReferenceMatch[4].replace(/[\\^${}[\]().*+?|]/g,""\\$&"")).replace(""jsStackTrace"",""[^\\n]+""))";

    private static readonly string StackHeadCompact = StackRef
        + "var stackTraceReferenceMatch=jsStackTrace().match(new RegExp(stackTraceReference));"
        + "if(stackTraceReferenceMatch)Module.stackTraceRegExp=" + StackRhs + ";";

    private static readonly string StackHeadSpaced = StackRef + "\n"
        + "var stackTraceReferenceMatch = jsStackTrace().match(new RegExp(stackTraceReference));\n"
        + "if (stackTraceReferenceMatch) Module.stackTraceRegExp = " + StackRhs + ";\n";

    // 바깥 framework 함수 모양: jsStackTrace 호출 횟수를 세는 spy 를 둔다.
    private static string StackFramework(string head)
    {
        return "function unityFramework(Module){var spyCalls=0;"
            + "function jsStackTrace(){spyCalls++;return \"Error\\n    at jsStackTrace (http://h/x.framework.js:10:5)\\n    at f (http://h/x.framework.js:20:3)\"}"
            + head
            + "return {calls:function(){return spyCalls}}}";
    }

    [TestCase(false)]
    [TestCase(true)]
    public void StackLazy_AppliesExactlyOnce_BothSpacingVariants(bool spaced)
    {
        string src = StackFramework(spaced ? StackHeadSpaced : StackHeadCompact);

        var r = AITFrameworkPatcher.PatchText(src, false, 10f, FakePayload, patchAudio: false, lazyStackTrace: true);

        CollectionAssert.AreEqual(new[] { AITFrameworkPatcher.GroupStackTraceLazy }, r.Applied);
        Assert.IsEmpty(r.Skipped);
        Assert.AreEqual(1, Count(r.Source, AITFrameworkPatcher.StackLazyMarker));
        StringAssert.Contains("Object.defineProperty(Module,\"stackTraceRegExp\"", r.Source);
        StringAssert.Contains("var stackTraceReferenceMatch;(function(){", r.Source);
        // 우변은 정확히 한 번, getter 안에만 남는다. 최상위의 jsStackTrace().match 대입은 사라진다.
        Assert.AreEqual(1, Count(r.Source, "stackTraceReferenceMatch[4]"));
        Assert.AreEqual(1, Count(r.Source, "jsStackTrace().match("), "최상위 호출은 getter 안으로 옮겨졌어야 한다");
        Assert.AreEqual(0, Count(r.Source, "Module.stackTraceRegExp="));
        StringAssert.EndsWith("return {calls:function(){return spyCalls}}}", r.Source, "뒤따르는 코드는 그대로여야 한다");
    }

    [Test]
    public void StackLazy_IsIdempotent_AndMarkerIsIndependentOfAudioMarker()
    {
        string src = StackFramework(StackHeadCompact);
        var first = AITFrameworkPatcher.PatchText(src, false, 10f, FakePayload, patchAudio: false, lazyStackTrace: true);

        var second = AITFrameworkPatcher.PatchText(first.Source, false, 10f, FakePayload, patchAudio: true, lazyStackTrace: true);

        Assert.IsTrue(second.AlreadyPatched);
        Assert.IsEmpty(second.Applied);
        Assert.AreEqual(first.Source, second.Source);
        Assert.IsFalse(AITFrameworkPatcher.HasAudioMarker(first.Source), "stacktrace-lazy 만 적용한 텍스트에 오디오 마커가 있으면 안 된다");
    }

    [Test]
    public void StackLazy_Disabled_LeavesTextUnchanged_AndDoesNotEvaluateGroup()
    {
        string src = StackFramework(StackHeadCompact);

        var r = AITFrameworkPatcher.PatchText(src, false, 10f, FakePayload, patchAudio: false, lazyStackTrace: false);

        Assert.IsEmpty(r.Applied);
        Assert.IsEmpty(r.Skipped, "끈 그룹은 건너뜀 사유도 남기지 않는다");
        Assert.AreSame(src, r.Source);

        // 기존 4인자 호출은 오디오만 평가한다(기본 stacktrace-lazy 꺼짐).
        var legacy = AITFrameworkPatcher.PatchText(SyntheticFramework() + StackHeadCompact, false, 10f, FakePayload);
        CollectionAssert.DoesNotContain(legacy.Applied, AITFrameworkPatcher.GroupStackTraceLazy);
        StringAssert.DoesNotContain(AITFrameworkPatcher.StackLazyMarker, legacy.Source);
    }

    [Test]
    public void StackLazy_AnchorMissing_SkipsOnlyThisGroup_AudioStillApplies()
    {
        string src = SyntheticFramework() + StackFramework("var unrelated=1;");

        var r = AITFrameworkPatcher.PatchText(src, false, 10f, FakePayload, patchAudio: true, lazyStackTrace: true);

        CollectionAssert.DoesNotContain(r.Applied, AITFrameworkPatcher.GroupStackTraceLazy);
        Assert.IsTrue(r.Skipped.Any(x => x.StartsWith(AITFrameworkPatcher.GroupStackTraceLazy + ":") && x.Contains("0회")), string.Join("|", r.Skipped));
        CollectionAssert.Contains(r.Applied, AITFrameworkPatcher.GroupClipMeta);
        CollectionAssert.Contains(r.Applied, AITFrameworkPatcher.GroupPrologue);
        StringAssert.DoesNotContain(AITFrameworkPatcher.StackLazyMarker, r.Source);
    }

    [Test]
    public void StackLazy_AnchorTwice_SkipsOnlyThisGroup_AudioStillApplies()
    {
        string src = SyntheticFramework() + StackFramework(StackHeadCompact) + StackFramework(StackHeadSpaced);

        var r = AITFrameworkPatcher.PatchText(src, false, 10f, FakePayload, patchAudio: true, lazyStackTrace: true);

        CollectionAssert.DoesNotContain(r.Applied, AITFrameworkPatcher.GroupStackTraceLazy);
        Assert.IsTrue(r.Skipped.Any(x => x.StartsWith(AITFrameworkPatcher.GroupStackTraceLazy + ":") && x.Contains("2회")), string.Join("|", r.Skipped));
        CollectionAssert.Contains(r.Applied, AITFrameworkPatcher.GroupClipMeta);
        Assert.AreEqual(2, Count(r.Source, "jsStackTrace().match("), "건너뛴 그룹은 어떤 편집도 남기지 않는다");
        StringAssert.DoesNotContain(AITFrameworkPatcher.StackLazyMarker, r.Source);
    }

    [Test]
    public void StackLazy_AppliesWhenAudioPatchDisabled_AndAudioGroupsAreNotEvaluated()
    {
        // 오디오 앵커가 모두 있어도 patchAudio=false 면 오디오 그룹은 건드리지 않는다.
        string src = SyntheticFramework() + StackFramework(StackHeadCompact);

        var r = AITFrameworkPatcher.PatchText(src, false, 10f, FakePayload, patchAudio: false, lazyStackTrace: true);

        CollectionAssert.AreEqual(new[] { AITFrameworkPatcher.GroupStackTraceLazy }, r.Applied);
        Assert.IsEmpty(r.Skipped);
        StringAssert.DoesNotContain("aitCfg", r.Source);
        StringAssert.DoesNotContain("aitInfo", r.Source);
        Assert.AreEqual(1, Count(r.Source, Anchor(AITFrameworkPatcher.GroupClipMeta, 0)), "오디오 앵커는 원문 그대로여야 한다");
    }

    [Test]
    public void StackLazy_AppliesAfterAudioGroups_InOrder()
    {
        string src = SyntheticFramework() + StackFramework(StackHeadSpaced);

        var r = AITFrameworkPatcher.PatchText(src, false, 10f, FakePayload, patchAudio: true, lazyStackTrace: true);

        Assert.AreEqual(AITFrameworkPatcher.GroupStackTraceLazy, r.Applied.Last());
        CollectionAssert.Contains(r.Applied, AITFrameworkPatcher.GroupPrologue);
        Assert.AreEqual(1, Count(r.Source, AITFrameworkPatcher.MarkerPrefix));
        Assert.AreEqual(1, Count(r.Source, AITFrameworkPatcher.StackLazyMarker));
    }

    [Test]
    public void StackLazy_Scanner_IgnoresParensInsideStringAndRegexLiterals()
    {
        // 우변 안에 문자열 ")" 와 정규식 리터럴 클래스 [)(] 가 있어도 닫는 괄호를 정확히 찾는다.
        const string rhs = @"new RegExp(""a)""+stackTraceReferenceMatch[4].replace(/[)(]/g,"""")+'(' /* ) */)";
        string head = StackRef + "var stackTraceReferenceMatch=jsStackTrace().match(new RegExp(stackTraceReference));"
            + "if(stackTraceReferenceMatch)Module.stackTraceRegExp=" + rhs + ";var after=1;";

        bool ok = AITFrameworkPatcher.TryPatchStackTraceLazy(StackFramework(head), out string patched, out string reason);

        Assert.IsTrue(ok, reason);
        StringAssert.Contains("c=" + rhs + "}catch(e){}", patched);
        StringAssert.Contains(AITFrameworkPatcher.StackLazyMarker + "var after=1;", patched, "';' 는 소비되고 뒤 문장은 보존된다");
        Assert.AreEqual(1, Count(patched, AITFrameworkPatcher.StackLazyMarker));
    }

    [Test]
    public void StackLazy_RhsWithoutSemicolon_EndsAtBraceOrNewline()
    {
        string noSemi = StackRef + "var stackTraceReferenceMatch=jsStackTrace().match(new RegExp(stackTraceReference));"
            + "if(stackTraceReferenceMatch)Module.stackTraceRegExp=" + StackRhs;
        string src = "function w(Module){" + noSemi + "}";

        Assert.IsTrue(AITFrameworkPatcher.TryPatchStackTraceLazy(src, out string patched, out string reason), reason);
        StringAssert.EndsWith(AITFrameworkPatcher.StackLazyMarker + "}", patched);

        string withNewline = "function w(Module){" + noSemi + "\nvar z=2}";
        Assert.IsTrue(AITFrameworkPatcher.TryPatchStackTraceLazy(withNewline, out patched, out reason), reason);
        StringAssert.Contains(AITFrameworkPatcher.StackLazyMarker + "\nvar z=2}", patched);
    }

    [Test]
    public void StackLazy_RhsContinuesPastCall_IsSkippedUntouched()
    {
        // new RegExp(...) 뒤에 식이 더 이어지면 우변 끝을 확신할 수 없으므로 건너뛴다.
        string head = StackRef + "var stackTraceReferenceMatch=jsStackTrace().match(new RegExp(stackTraceReference));"
            + "if(stackTraceReferenceMatch)Module.stackTraceRegExp=" + StackRhs + ".valueOf();";
        string src = StackFramework(head);

        var r = AITFrameworkPatcher.PatchText(src, false, 10f, FakePayload, patchAudio: false, lazyStackTrace: true);

        Assert.IsEmpty(r.Applied);
        Assert.IsTrue(r.Skipped.Any(x => x.StartsWith(AITFrameworkPatcher.GroupStackTraceLazy + ":")), string.Join("|", r.Skipped));
        Assert.AreSame(src, r.Source);

        // 닫히지 않는 괄호도 같다.
        string unclosed = StackRef + "var stackTraceReferenceMatch=jsStackTrace().match(new RegExp(stackTraceReference));"
            + "if(stackTraceReferenceMatch)Module.stackTraceRegExp=new RegExp(\"a\"";
        Assert.IsFalse(AITFrameworkPatcher.TryPatchStackTraceLazy(unclosed, out string p2, out _));
        Assert.AreSame(unclosed, p2);
    }

    [Test]
    public void StackLazy_TriState_AndEnvOverride()
    {
        var config = ScriptableObject.CreateInstance<AITEditorScriptObject>();
        try
        {
            Assert.AreEqual(-1, config.frameworkLazyStackTraceMode, "기본값은 자동(-1)이다");
            Assert.IsTrue(AITFrameworkPatcher.EffectiveLazyStackTrace(config), "자동 = ON");
            Assert.IsTrue(AITFrameworkPatcher.EffectiveLazyStackTrace(null), "config 가 없어도 자동과 같다");

            config.frameworkLazyStackTraceMode = 0;
            Assert.IsFalse(AITFrameworkPatcher.EffectiveLazyStackTrace(config));
            config.frameworkLazyStackTraceMode = 1;
            Assert.IsTrue(AITFrameworkPatcher.EffectiveLazyStackTrace(config));

            // 환경 변수가 설정보다 우선한다. 이상한 값은 설정값으로 폴백(경고만).
            foreach (var (env, setting, expected) in new[]
            {
                ("1", 0, true), ("true", 0, true), ("TRUE", 0, true),
                ("0", 1, false), ("false", 1, false), (" False ", -1, false),
            })
            {
                Environment.SetEnvironmentVariable(AITFrameworkPatcher.LazyStackTraceEnvVar, env);
                config.frameworkLazyStackTraceMode = setting;
                Assert.AreEqual(expected, AITFrameworkPatcher.EffectiveLazyStackTrace(config), "env='" + env + "' setting=" + setting);
            }

            Environment.SetEnvironmentVariable(AITFrameworkPatcher.LazyStackTraceEnvVar, "maybe");
            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(AITFrameworkPatcher.LazyStackTraceEnvVar));
            config.frameworkLazyStackTraceMode = 0;
            Assert.IsFalse(AITFrameworkPatcher.EffectiveLazyStackTrace(config), "잘못된 env 는 무시하고 설정값(0)");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(config);
        }
    }

    // ─────────────────────────── stacktrace-lazy: 파일 파이프라인(내장 Node 필요) ───────────────────────────

    [Test]
    public void StackLazy_Apply_AudioOff_StillPatches_AndRenames()
    {
        RequireNode(out string node);
        string name = WriteFramework(node, "z.framework.js", "br", StackFramework(StackHeadCompact));
        var renames = new Dictionary<string, string>();

        // audioForceCompressedPlayback=0 이어도 stacktrace-lazy 가 켜져 있으면 ApplyCore 는 계속한다.
        int n = AITFrameworkPatcher.Apply(_tempDir, NewConfig(0, -1), renames);

        Assert.AreEqual(1, n);
        string patchedName = renames[name];
        StringAssert.IsMatch(@"^z\.aitp1-[0-9a-f]{8}\.framework\.js\.br$", patchedName);
        string patched = ReadFramework(node, Path.Combine(_tempDir, patchedName), "br");
        StringAssert.Contains(AITFrameworkPatcher.StackLazyMarker, patched);
        StringAssert.DoesNotContain(AITFrameworkPatcher.MarkerPrefix, patched, "오디오 마커는 없어야 한다");
        Assert.IsFalse(AITFrameworkPatcher.LastApplyClipMetaApplied, "오디오 패치가 없으니 clip-meta 적용 기록은 false");
    }

    [Test]
    public void StackLazy_Apply_ModeZero_AudioOff_TouchesNothing()
    {
        RequireNode(out string node);
        string name = WriteFramework(node, "o.framework.js", "none", StackFramework(StackHeadCompact));
        string before = File.ReadAllText(Path.Combine(_tempDir, name));
        var renames = new Dictionary<string, string>();

        Assert.AreEqual(0, AITFrameworkPatcher.Apply(_tempDir, NewConfig(0, 0), renames));

        Assert.IsEmpty(renames);
        Assert.AreEqual(before, File.ReadAllText(Path.Combine(_tempDir, name)));
    }

    [Test]
    public void StackLazy_Apply_ModeZero_AudioOn_DoesNotAddStackPatch()
    {
        RequireNode(out string node);
        string name = WriteFramework(node, "m.framework.js", "none", SyntheticFramework() + StackFramework(StackHeadCompact));
        var renames = new Dictionary<string, string>();

        Assert.AreEqual(1, AITFrameworkPatcher.Apply(_tempDir, NewConfig(-1, 0), renames));

        string patched = File.ReadAllText(Path.Combine(_tempDir, renames[name]));
        StringAssert.Contains(AITFrameworkPatcher.MarkerPrefix, patched);
        StringAssert.DoesNotContain(AITFrameworkPatcher.StackLazyMarker, patched);
    }

    [Test]
    public void StackLazy_Apply_ChangesPatchedName_ComparedToAudioOnly()
    {
        RequireNode(out string node);
        string source = SyntheticFramework() + StackFramework(StackHeadCompact);
        string name = WriteFramework(node, "h.framework.js", "none", source);
        var withLazy = new Dictionary<string, string>();
        Assert.AreEqual(1, AITFrameworkPatcher.Apply(_tempDir, NewConfig(-1, 1), withLazy));

        File.Delete(Path.Combine(_tempDir, withLazy[name]));
        WriteFramework(node, "h.framework.js", "none", source);
        var withoutLazy = new Dictionary<string, string>();
        Assert.AreEqual(1, AITFrameworkPatcher.Apply(_tempDir, NewConfig(-1, 0), withoutLazy));

        Assert.AreNotEqual(withLazy[name], withoutLazy[name], "적용 그룹이 다르면 패치 바이트도 다르므로 파일명이 달라야 캐시가 섞이지 않는다");
    }

    [Test]
    public void StackLazy_PatchedSnippet_ComputesRegExpOnFirstAccessOnly_AndSetterWorks()
    {
        RequireNode(out string node);
        var r = AITFrameworkPatcher.PatchText(
            StackFramework(StackHeadSpaced), false, 10f, FakePayload, patchAudio: false, lazyStackTrace: true);
        Assert.AreEqual(1, r.Applied.Count, string.Join("|", r.Skipped));

        string harness = r.Source + "\n"
            + "var out=[];\n"
            // 1) eval 직후 spy 0회, 첫 접근 뒤 1회, 두 번째 접근은 캐시(여전히 1회), RegExp 이고 같은 스크립트 URL 의 줄만 매치.
            + "var M1={};var fw1=unityFramework(M1);out.push(fw1.calls());\n"
            + "var re=M1.stackTraceRegExp;out.push(fw1.calls());\n"
            + "out.push(re instanceof RegExp);\n"
            + "out.push(re.test('\\n    at f (http://h/x.framework.js:20:3)'));\n"
            + "out.push(re.test('\\n    at g (http://h/other.js:20:3)'));\n"
            + "var re2=M1.stackTraceRegExp;out.push(re2===re);out.push(fw1.calls());\n"
            // 2) setter: 접근 전에 대입하면 spy 는 끝까지 0회이고 대입값이 그대로 돌아온다.
            + "var M2={};var fw2=unityFramework(M2);M2.stackTraceRegExp=/zz/;\n"
            + "out.push(M2.stackTraceRegExp.source);out.push(fw2.calls());\n"
            // 3) 열거 가능하고 재정의 가능(stock 의 일반 프로퍼티와 가장 가까운 모양).
            + "var d=Object.getOwnPropertyDescriptor(M1,'stackTraceRegExp');out.push(d.enumerable&&d.configurable);\n"
            + "console.log(out.join('|'));\n";

        string stdout = RunNodeScript(node, harness);

        Assert.AreEqual("0|1|true|true|false|true|1|zz|0|true", stdout.Trim());
    }

    [Test]
    public void StackLazy_PatchedSnippet_SwallowsErrorsInsideGetter()
    {
        RequireNode(out string node);
        // jsStackTrace 가 던져도 최상위(stock)에서는 부팅이 죽지만, 지연 getter 는 오류 처리 경로를 깨지 않도록 undefined 를 돌려준다.
        string src = "function unityFramework(Module){function jsStackTrace(){throw new Error('boom')}" + StackHeadCompact + "}";
        var r = AITFrameworkPatcher.PatchText(src, false, 10f, FakePayload, patchAudio: false, lazyStackTrace: true);
        Assert.AreEqual(1, r.Applied.Count, string.Join("|", r.Skipped));

        string stdout = RunNodeScript(node, r.Source + "\nvar M={};unityFramework(M);console.log(String(M.stackTraceRegExp)+'|'+String(M.stackTraceRegExp));\n");

        Assert.AreEqual("undefined|undefined", stdout.Trim());
    }

    private string RunNodeScript(string node, string script)
    {
        string path = Path.Combine(_tempDir, "run-" + Guid.NewGuid().ToString("N").Substring(0, 6) + ".js");
        File.WriteAllText(path, script, new UTF8Encoding(false));
        var psi = new ProcessStartInfo
        {
            FileName = node,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = _tempDir,
        };
        psi.ArgumentList.Add(path);
        using (var p = Process.Start(psi))
        {
            string err = p.StandardError.ReadToEnd();
            string output = p.StandardOutput.ReadToEnd();
            Assert.IsTrue(p.WaitForExit(60000), "node 실행 시간 초과");
            Assert.AreEqual(0, p.ExitCode, "node 실행 실패: " + err);
            return output;
        }
    }
}
