// -----------------------------------------------------------------------
// AITLoaderPatcherTests.cs - loader 패처(AITLoaderPatcher, 소비한 data 버퍼 해제 훅)와 ait-datarelease.js 계약 검증
//
// 이 저장소는 공개라서 Unity loader 원본을 커밋하지 않는다. 합성 텍스트는 패처가 매칭하는 짧은 패턴만 담는다
// (실제 3개 Unity 버전 loader 로는 로컬에서 따로 확인했다).
//   · 순수 패치 엔진(PatchText): 두 앵커가 정확히 1회일 때만 둘 다 적용, 변수 이름이 달라도(2021.3 / 6000.x 모양) 적용,
//     FS_createDataFile 이 buffer 할당보다 앞에 있어도 적용, 0회/2회/한쪽만 어긋나면 통째로 건너뜀, 멱등, CRLF 보존
//   · 파일 파이프라인(Apply): 자동(-1)/exactDataBody 꺼짐/.unityweb/이미 패치된 이름은 건드리지 않고,
//     켜져 있으면 Node 로 --check 를 통과한 뒤 ".aitpN-<hash>" 이름으로 옮긴다(Node 없으면 건너뜀)
//   · 템플릿 계약: ait-datarelease.js 가 로더 패치가 부르는 훅을 정의하고, 읽는 __AIT_PERF 키가 AITPerfFlags 와 일치하며,
//     index.html 에서 ait-databuf.js 다음·Unity 로더 앞에 정확히 한 번 로드된다
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using AppsInToss;
using AppsInToss.Editor;
using AppsInToss.Editor.Package;

[TestFixture]
[Category("Unit")]
public class AITLoaderPatcherTests
{
    // 6000.x 모양: 할당이 앞, FS_createDataFile 이 뒤.
    private const string Synth6000 =
        "function createUnityInstance(r,t,l){var g={};g.readBodyWithProgress=function(a,i,s){var e=a.body?a.body.getReader():void 0,l=1,d=parseInt(a.headers.get(\"Content-Length\")),u=new Uint8Array(d),c=[],h=0,f=0;return u};"
        + "g.preRun.push(function(){e.then(function(r){for(;t<s;){var l=n(),d=n(),u=n(),c=x();g.FS_createPath(c,c,!0,!0);g.FS_createDataFile(c,null,r.subarray(l,l+d),!0,!0,!0)}})})}";

    // 2021.3 모양: FS_createDataFile 이 buffer 할당보다 앞에 있다(변수 이름도 다르다).
    private const string Synth2021 =
        "function createUnityInstance(r,t,l){var c={};c.preRun.push(function(){t.then(function(e){for(;n<o;){var a=r.getUint32(n,!0),i=1,s=2,l=String.fromCharCode.apply(null,e.subarray(n,n+s));c.FS_createDataFile(l,null,e.subarray(a,a+i),!0,!0,!0)}})});"
        + "c.readBodyWithProgress=function(a,i,s){var e=a.body?a.body.getReader():void 0,l=1,d=parseInt(a.headers.get(\"Content-Length\")),u=new Uint8Array(d),c=[],f=0,h=0;return u}}";

    private const string Alloc6000Expected =
        ",u=(window.__AIT_DATAREL&&window.__AIT_DATAREL.alloc(d,a))||new Uint8Array(d),c=[],";

    private string _tempDir;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ait-loaderpatcher-" + Guid.NewGuid().ToString("N").Substring(0, 8));
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

    // ─────────────────────────── 패치 엔진 ───────────────────────────

    [Test]
    public void PatchText_6000Shape_AppliesBothEdits()
    {
        AITLoaderPatcher.PatchOutcome o = AITLoaderPatcher.PatchText(Synth6000);

        Assert.IsTrue(o.Applied, o.Reason);
        StringAssert.Contains(Alloc6000Expected, o.Source);
        // 호출 인자는 그대로, 반환값은 래퍼가 그대로 돌려준다. 호출은 한 번만 남는다(인자가 한 번만 평가된다).
        StringAssert.Contains(
            "(window.__AIT_DATAREL&&window.__AIT_DATAREL.created||function(n){return n})(g.FS_createDataFile(c,null,r.subarray(l,l+d),!0,!0,!0),c)",
            o.Source);
        Assert.AreEqual(1, Regex.Matches(o.Source, Regex.Escape("FS_createDataFile(")).Count);
        // 훅이 없으면(스크립트 미로드) 원래 할당으로 돌아간다.
        StringAssert.Contains("||new Uint8Array(d)", o.Source);
    }

    [Test]
    public void PatchText_2021Shape_CreateBeforeAlloc_AppliesBothEdits()
    {
        AITLoaderPatcher.PatchOutcome o = AITLoaderPatcher.PatchText(Synth2021);

        Assert.IsTrue(o.Applied, o.Reason);
        StringAssert.Contains(
            "(window.__AIT_DATAREL&&window.__AIT_DATAREL.created||function(n){return n})(c.FS_createDataFile(l,null,e.subarray(a,a+i),!0,!0,!0),l)",
            o.Source);
        StringAssert.Contains(Alloc6000Expected, o.Source);
        // 앞쪽 구간을 먼저 고쳐도 뒤쪽 오프셋이 어긋나지 않았는지: 바깥 구조가 보존된다.
        StringAssert.StartsWith("function createUnityInstance(r,t,l){var c={};c.preRun.push(", o.Source);
        StringAssert.EndsWith("return u}}", o.Source);
    }

    [Test]
    public void PatchText_RespVariableFollowsReadBodyFirstParameter()
    {
        string src = Synth6000.Replace("readBodyWithProgress=function(a,i,s)", "readBodyWithProgress=function(q,i,s)");

        AITLoaderPatcher.PatchOutcome o = AITLoaderPatcher.PatchText(src);

        Assert.IsTrue(o.Applied, o.Reason);
        StringAssert.Contains("window.__AIT_DATAREL.alloc(d,q)", o.Source);
    }

    [Test]
    public void PatchText_IsIdempotent()
    {
        AITLoaderPatcher.PatchOutcome first = AITLoaderPatcher.PatchText(Synth6000);
        AITLoaderPatcher.PatchOutcome second = AITLoaderPatcher.PatchText(first.Source);

        Assert.IsTrue(first.Applied);
        Assert.IsFalse(second.Applied);
        Assert.IsTrue(second.AlreadyPatched);
        Assert.AreEqual(first.Source, second.Source);
    }

    [Test]
    public void PatchText_MissingAnchors_LeaveTextUntouched()
    {
        string noRead = Synth6000.Replace("readBodyWithProgress", "readBodyOther");
        string noAlloc = Synth6000.Replace("u=new Uint8Array(d)", "u=makeBuf(d)");
        string noCreate = Synth6000.Replace("FS_createDataFile", "FS_createFile");

        foreach (string src in new[] { noRead, noAlloc, noCreate })
        {
            AITLoaderPatcher.PatchOutcome o = AITLoaderPatcher.PatchText(src);

            Assert.IsFalse(o.Applied);
            Assert.IsFalse(o.AlreadyPatched);
            Assert.IsNotEmpty(o.Reason);
            Assert.AreEqual(src, o.Source, "앵커가 없으면 텍스트를 바꾸지 않아야 합니다.");
        }
    }

    [Test]
    public void PatchText_AmbiguousAnchors_LeaveTextUntouched()
    {
        // 할당 앵커가 창 안에 두 번, FS_createDataFile 패턴이 두 번.
        string dupAlloc = Synth6000.Replace("return u};", "var z=1,w=new Uint8Array(d),y=[],k=2;return u};");
        string dupCreate = Synth6000 + ";g.FS_createDataFile(c,null,r.subarray(l,l+d),!0,!0,!0)";
        string dupRead = Synth6000 + ";g.readBodyWithProgress=function(a,i,s){}";

        foreach (string src in new[] { dupAlloc, dupCreate, dupRead })
        {
            AITLoaderPatcher.PatchOutcome o = AITLoaderPatcher.PatchText(src);

            Assert.IsFalse(o.Applied, "모호한 앵커는 적용하면 안 됩니다: " + o.Reason);
            Assert.AreEqual(src, o.Source);
        }
    }

    [Test]
    public void PatchText_AllocFarFromReadBody_IsNotMatched()
    {
        // 할당 패턴이 readBodyWithProgress 시작에서 멀리 떨어져 있으면(다른 함수) 잡지 않는다.
        string filler = new string('x', 4000);
        string src = Synth6000.Replace("return u};", "return u};var pad=\"" + filler + "\";var o=1,k=new Uint8Array(d),y=[],z=2;")
            .Replace(",u=new Uint8Array(d),c=[],", ",u=makeBuf(d),c=[],");

        AITLoaderPatcher.PatchOutcome o = AITLoaderPatcher.PatchText(src);

        Assert.IsFalse(o.Applied);
        Assert.AreEqual(src, o.Source);
    }

    [Test]
    public void PatchText_PreservesCrLf()
    {
        string src = "// header\r\n" + Synth6000.Replace(";g.preRun", ";\r\ng.preRun") + "\r\n";

        AITLoaderPatcher.PatchOutcome o = AITLoaderPatcher.PatchText(src);

        Assert.IsTrue(o.Applied, o.Reason);
        Assert.AreEqual(Regex.Matches(src, "\r\n").Count, Regex.Matches(o.Source, "\r\n").Count, "줄바꿈을 바꾸지 않아야 합니다.");
        Assert.AreEqual(Regex.Matches(src, "\n").Count, Regex.Matches(o.Source, "\n").Count);
    }

    [Test]
    public void PatchText_EmptyOrNull_DoesNothing()
    {
        Assert.IsFalse(AITLoaderPatcher.PatchText(null).Applied);
        Assert.IsFalse(AITLoaderPatcher.PatchText(string.Empty).Applied);
    }

    // ─────────────────────────── 파일 파이프라인 ───────────────────────────

    [Test]
    public void Apply_AutoSetting_TouchesNothing()
    {
        string js = Path.Combine(_tempDir, "abc.loader.js");
        File.WriteAllText(js, Synth6000);
        var renames = new Dictionary<string, string>();

        int n = AITLoaderPatcher.Apply(_tempDir, NewConfig(-1, -1), renames);

        Assert.AreEqual(0, n);
        Assert.IsEmpty(renames);
        Assert.AreEqual(Synth6000, File.ReadAllText(js));
    }

    [Test]
    public void Apply_ExplicitOff_TouchesNothing()
    {
        string js = Path.Combine(_tempDir, "abc.loader.js");
        File.WriteAllText(js, Synth6000);
        var renames = new Dictionary<string, string>();

        int n = AITLoaderPatcher.Apply(_tempDir, NewConfig(0, -1), renames);

        Assert.AreEqual(0, n);
        Assert.IsEmpty(renames);
        Assert.AreEqual(Synth6000, File.ReadAllText(js));
    }

    [Test]
    public void Apply_ExactDataBodyOff_TouchesNothing()
    {
        string js = Path.Combine(_tempDir, "abc.loader.js");
        File.WriteAllText(js, Synth6000);
        var renames = new Dictionary<string, string>();

        // 훅은 data 크기가 정확해야(exactDataBody) 아무것이나 할 수 있다. 꺼져 있으면 패치하지 않는다.
        int n = AITLoaderPatcher.Apply(_tempDir, NewConfig(1, 0), renames);

        Assert.AreEqual(0, n);
        Assert.IsEmpty(renames);
        Assert.AreEqual(Synth6000, File.ReadAllText(js));
    }

    [Test]
    public void Apply_UnitywebArtifact_TouchesNothing()
    {
        string js = Path.Combine(_tempDir, "abc.loader.js");
        File.WriteAllText(js, Synth6000);
        File.WriteAllBytes(Path.Combine(_tempDir, "abc.data.unityweb"), new byte[] { 1, 2, 3 });
        var renames = new Dictionary<string, string>();

        int n = AITLoaderPatcher.Apply(_tempDir, NewConfig(1, -1), renames);

        Assert.AreEqual(0, n);
        Assert.IsEmpty(renames);
        Assert.AreEqual(Synth6000, File.ReadAllText(js));
    }

    [Test]
    public void Apply_AlreadyPatchedName_IsSkipped()
    {
        string js = Path.Combine(_tempDir, "abc.aitp1-0a1b2c3d.loader.js");
        File.WriteAllText(js, Synth6000);
        var renames = new Dictionary<string, string>();

        int n = AITLoaderPatcher.Apply(_tempDir, NewConfig(1, -1), renames);

        Assert.AreEqual(0, n);
        Assert.IsEmpty(renames);
        Assert.AreEqual(Synth6000, File.ReadAllText(js));
    }

    [Test]
    public void Apply_NullOrMissingDirectory_ReturnsZero()
    {
        Assert.AreEqual(0, AITLoaderPatcher.Apply(null, NewConfig(1, -1), null));
        Assert.AreEqual(0, AITLoaderPatcher.Apply(Path.Combine(_tempDir, "nope"), NewConfig(1, -1), null));
        Assert.AreEqual(0, AITLoaderPatcher.Apply(_tempDir, null, null));
    }

    [Test]
    public void Apply_UnmatchedLoader_KeepsOriginal()
    {
        string js = Path.Combine(_tempDir, "abc.loader.js");
        File.WriteAllText(js, "function createUnityInstance(){return 1}");
        var renames = new Dictionary<string, string>();

        int n = AITLoaderPatcher.Apply(_tempDir, NewConfig(1, -1), renames);

        Assert.AreEqual(0, n);
        Assert.IsEmpty(renames);
        Assert.AreEqual("function createUnityInstance(){return 1}", File.ReadAllText(js));
    }

    [Test]
    public void Apply_Enabled_PatchesRenamesAndRecordsIt()
    {
        RequireNode();
        string js = Path.Combine(_tempDir, "abc.loader.js");
        File.WriteAllText(js, Synth6000);
        var renames = new Dictionary<string, string>();

        int n = AITLoaderPatcher.Apply(_tempDir, NewConfig(1, -1), renames);

        Assert.AreEqual(1, n);
        Assert.IsFalse(File.Exists(js), "원본 이름은 지워져야 합니다.");
        Assert.IsTrue(renames.TryGetValue("abc.loader.js", out string newName));
        Assert.IsTrue(Regex.IsMatch(newName, @"^abc\.aitp\d+-[0-9a-f]{8}\.loader\.js$"), newName);
        Assert.IsTrue(AITPatchedFileNaming.IsPatched(newName));
        string patched = File.ReadAllText(Path.Combine(_tempDir, newName));
        StringAssert.Contains("window.__AIT_DATAREL", patched);

        // 한 번 더 돌려도(patch-set 이름이라) 다시 패치하지 않는다.
        var again = new Dictionary<string, string>();
        Assert.AreEqual(0, AITLoaderPatcher.Apply(_tempDir, NewConfig(1, -1), again));
        Assert.IsEmpty(again);
    }

    [Test]
    public void Apply_Enabled_RewritesHtmlReferences()
    {
        RequireNode();
        string js = Path.Combine(_tempDir, "abc.loader.js");
        File.WriteAllText(js, Synth6000);
        string html = Path.Combine(_tempDir, "index.html");
        File.WriteAllText(html, "<script src=\"Build/abc.loader.js\"></script>");

        var renames = new Dictionary<string, string>();
        int n = AITLoaderPatcher.Apply(_tempDir, NewConfig(1, -1), renames);

        Assert.AreEqual(1, n);
        string newName = renames["abc.loader.js"];
        StringAssert.Contains("Build/" + newName, File.ReadAllText(html));
    }

    // ─────────────────────────── 템플릿 계약 ───────────────────────────

    [Test]
    public void AitDataRelease_DefinesHooksTheLoaderPatchCalls()
    {
        string js = ReadDataRelease();

        StringAssert.DoesNotContain("__stub", js);
        Assert.AreEqual("window.__AIT_DATAREL", AITLoaderPatcher.HookGlobal);
        Assert.IsTrue(Regex.IsMatch(js, @"window\.__AIT_DATAREL\s*=\s*\{"), "ait-datarelease.js 가 window.__AIT_DATAREL 을 정의하지 않습니다.");
        foreach (string member in new[] { "alloc: alloc", "created: created", "getState" })
        {
            StringAssert.Contains(member, js, "훅 객체에 '" + member + "' 이 없습니다.");
        }
        // 패처가 심는 호출: alloc(size, resp), created(node, path)
        Assert.IsTrue(Regex.IsMatch(js, @"function alloc\(d,\s*resp\)"));
        Assert.IsTrue(Regex.IsMatch(js, @"function created\(node,\s*path\)"));
    }

    [Test]
    public void AitDataRelease_ReadsOnlyPerfKeys_ThatAITPerfFlagsEmits()
    {
        string js = ReadDataRelease();
        string flags = ReadPackageFile("Editor/Package/AITPerfFlags.cs");

        foreach (string key in new[] { "releaseConsumedData", "dataRawSize", "unityweb" })
        {
            Assert.IsTrue(Regex.IsMatch(js, @"perf\." + key + @"\b"), "ait-datarelease.js 가 __AIT_PERF." + key + " 를 읽지 않습니다.");
            StringAssert.Contains("\"" + key + "\"", flags, "AITPerfFlags.ToJson 이 " + key + " 키를 내보내지 않습니다.");
        }
    }

    [Test]
    public void AitDataRelease_IsOptIn_FailOpen_AndGuardedByFeatureDetection()
    {
        string js = ReadDataRelease();

        // 명시적으로 true 일 때만 동작(키가 없거나 객체가 없으면 꺼짐).
        Assert.IsTrue(Regex.IsMatch(js, @"perf\.releaseConsumedData\s*!==\s*true"));
        // 크기 조절 ArrayBuffer 미지원 엔진에서는 stock.
        StringAssert.Contains("typeof ArrayBuffer.prototype.resize === 'function'", js);
        // 로더가 던진 훅을 감싼 try/catch: alloc 은 실패하면 null 을 돌려준다.
        Assert.IsTrue(Regex.IsMatch(js, @"function alloc\(d,\s*resp\)\s*\{\s*try\s*\{"));
        // TextDecoder shim 은 구간이 끝나면 되돌린다.
        StringAssert.Contains("removeShim", js);
        // 해제 뒤 재읽기는 console.error 로 드러난다.
        StringAssert.Contains("late-read", js);
    }

    [Test]
    public void IndexHtml_LoadsAitDataRelease_AfterDatabuf_AndBeforeUnityLoader()
    {
        string html = ReadPackageFile("WebGLTemplates/AITTemplate/index.html");

        int databuf = html.IndexOf("<script src=\"Runtime/ait-databuf.js\"", StringComparison.Ordinal);
        int release = html.IndexOf("<script src=\"Runtime/ait-datarelease.js\"", StringComparison.Ordinal);
        int loader = html.IndexOf("%UNITY_WEBGL_LOADER_URL%", StringComparison.Ordinal);

        Assert.GreaterOrEqual(databuf, 0);
        Assert.GreaterOrEqual(release, 0, "index.html 에 ait-datarelease.js 스크립트 태그가 없습니다.");
        Assert.Less(databuf, release, "ait-datarelease.js 는 ait-databuf.js 다음에 로드합니다.");
        Assert.Less(release, loader, "ait-datarelease.js 는 Unity 로더(= 훅을 부르는 쪽)보다 먼저 로드돼야 합니다.");
        Assert.AreEqual(1, Regex.Matches(html, Regex.Escape("Runtime/ait-datarelease.js")).Count,
            "ait-datarelease.js 참조는 정확히 한 번이어야 합니다(주석에도 파일 경로를 반복해 적지 않습니다).");
    }

    // ─────────────────────────── 헬퍼 ───────────────────────────

    private static AITEditorScriptObject NewConfig(int releaseConsumedData, int exactDataBody)
    {
        var config = ScriptableObject.CreateInstance<AITEditorScriptObject>();
        config.releaseConsumedData = releaseConsumedData;
        config.exactDataBody = exactDataBody;
        return config;
    }

    private static void RequireNode()
    {
        if (!AITBrotliCompressor.TryResolveNode(out string node) || string.IsNullOrEmpty(node))
        {
            Assert.Ignore("내장 Node 미가용 — loader 패치 파이프라인 테스트 건너뜀");
        }
    }

    private static string ReadDataRelease()
    {
        return ReadPackageFile("WebGLTemplates/AITTemplate/Runtime/ait-datarelease.js");
    }

    private static string ReadPackageFile(string relativePath)
    {
        Assert.IsTrue(
            AITPackagePathResolver.TryResolveFile(relativePath, out string path, typeof(AITConvertCore)),
            relativePath + " 경로를 찾지 못했습니다.");
        Assert.IsTrue(File.Exists(path), "파일이 존재하지 않습니다: " + path);
        return File.ReadAllText(path);
    }
}
