// -----------------------------------------------------------------------
// AITPerfTemplateWiringTests.cs - 모바일 런타임 최적화 스캐폴딩의 템플릿/빌더 배선 검증
//   · %AIT_PERF_FLAGS% 는 템플릿에 정확히 한 번, 작은따옴표 JSON.parse 리터럴 안에, fail-open 으로 선언
//   · WebGLBuildCopier 가 같은 줄에서 AITJsStringEscaper.EscapeSingleQuoted 로 치환
//   · 런타임 스크립트 태그 순서(ait-mem 은 head, ait-gl 은 Unity 로더 앞), 스텁 파일 존재
//   · configure 호출이 createUnityInstance 앞, bindContextLoss/tierCap/unityConfig 노출
//   · 패치 호출 지점이 page cache·warm manifest·brotli 보다 앞
// -----------------------------------------------------------------------

using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using AppsInToss.Editor;

[TestFixture]
[Category("Unit")]
public class AITPerfTemplateWiringTests
{
    [Test]
    public void IndexHtml_DeclaresPerfFlagsPlaceholder_ExactlyOnce_InSingleQuotedJsonParse()
    {
        string html = ReadIndexHtml();

        Assert.AreEqual(1, Regex.Matches(html, Regex.Escape("%AIT_PERF_FLAGS%")).Count,
            "%AIT_PERF_FLAGS% 는 템플릿에 정확히 한 번만 나와야 합니다(주석에도 적지 않습니다).");
        StringAssert.Contains("JSON.parse('%AIT_PERF_FLAGS%')", html,
            "플레이스홀더는 작은따옴표 JSON.parse 리터럴 안에 있어야 AITJsStringEscaper 규칙과 맞습니다.");
    }

    [Test]
    public void IndexHtml_PerfFlags_FailOpenAndExposedAsWindowGlobal()
    {
        string html = ReadIndexHtml();

        Match m = Regex.Match(html, @"JSON\.parse\('%AIT_PERF_FLAGS%'\)\s*\|\|\s*\{\}\s*\}\s*catch\s*\(\s*e\s*\)\s*\{\s*return\s*\{\}\s*\}");
        Assert.IsTrue(m.Success, "JSON.parse 는 try/catch 로 감싸고 실패 시 {} 를 돌려줘야 합니다(fail-open).");
        StringAssert.Contains("window.__AIT_PERF = __AIT_PERF", html);
    }

    [Test]
    public void IndexHtml_PerfFlags_DefinedBeforeAnyRuntimeScript()
    {
        string html = ReadIndexHtml();

        int flags = html.IndexOf("window.__AIT_PERF = __AIT_PERF", System.StringComparison.Ordinal);
        int pageCache = html.IndexOf("%AIT_PAGE_CACHE_SCRIPT%", System.StringComparison.Ordinal);
        int earlyFetch = html.IndexOf("%AIT_EARLY_FETCH_SCRIPT%", System.StringComparison.Ordinal);
        int mem = html.IndexOf("Runtime/ait-mem.js", System.StringComparison.Ordinal);

        Assert.GreaterOrEqual(flags, 0);
        Assert.Less(flags, pageCache, "__AIT_PERF 는 페이지 캐시 스크립트보다 먼저 정의돼야 합니다.");
        Assert.Less(flags, earlyFetch);
        Assert.Less(flags, mem);
    }

    [Test]
    public void IndexHtml_ScriptTags_ArePresent_AndOrdered()
    {
        string html = ReadIndexHtml();

        foreach (string name in new[] { "ait-mem.js", "ait-pacing.js", "ait-databuf.js", "ait-gl.js" })
        {
            Assert.AreEqual(1, Regex.Matches(html, "<script src=\"Runtime/" + Regex.Escape(name) + "\"").Count,
                name + " 스크립트 태그가 정확히 하나여야 합니다.");
        }

        int unityLoader = html.IndexOf("%UNITY_WEBGL_LOADER_URL%", System.StringComparison.Ordinal);
        int createInstance = html.IndexOf("createUnityInstance(canvas, config", System.StringComparison.Ordinal);
        Assert.GreaterOrEqual(unityLoader, 0);
        Assert.GreaterOrEqual(createInstance, 0);

        Assert.Less(html.IndexOf("Runtime/ait-mem.js", System.StringComparison.Ordinal), html.IndexOf("</head>", System.StringComparison.Ordinal),
            "ait-mem.js 는 wasm 인스턴스화보다 먼저 설치되도록 head 에서 로드해야 합니다.");
        Assert.Less(html.IndexOf("Runtime/ait-gl.js", System.StringComparison.Ordinal), unityLoader,
            "ait-gl.js 는 Unity 로더(= context 생성)보다 먼저 로드돼야 합니다.");
    }

    [Test]
    public void IndexHtml_ConfigureCalls_PrecedeCreateUnityInstance()
    {
        string html = ReadIndexHtml();

        int create = html.IndexOf("createUnityInstance(canvas, config", System.StringComparison.Ordinal);
        int databuf = html.IndexOf("__AIT_DATABUF.configure(config)", System.StringComparison.Ordinal);
        int pacing = html.IndexOf("__AIT_PACING.configure(config)", System.StringComparison.Ordinal);

        Assert.GreaterOrEqual(databuf, 0, "__AIT_DATABUF.configure(config) 호출이 없습니다.");
        Assert.GreaterOrEqual(pacing, 0, "__AIT_PACING.configure(config) 호출이 없습니다.");
        Assert.Less(databuf, create);
        Assert.Less(pacing, create);
    }

    [Test]
    public void IndexHtml_ExposesUnityConfigAndEffectiveDpr_AndBindsContextLoss()
    {
        string html = ReadIndexHtml();

        StringAssert.Contains("window.unityConfig = config", html);
        StringAssert.Contains("window.__AIT_EFFECTIVE_DPR", html);
        StringAssert.Contains("__AIT_GL.tierCap", html);
        StringAssert.Contains("__AIT_GL.bindContextLoss(canvas)", html);
        StringAssert.Contains("touch-action: none", html);
    }

    [Test]
    public void RuntimeStubs_Exist_AndDefineTheirGlobals()
    {
        var expected = new (string file, string global)[]
        {
            ("ait-gl.js", "__AIT_GL"),
            ("ait-pacing.js", "__AIT_PACING"),
            ("ait-mem.js", "AITMemory"),
            ("ait-databuf.js", "__AIT_DATABUF"),
        };

        foreach (var e in expected)
        {
            string js = ReadPackageFile("WebGLTemplates/AITTemplate/Runtime/" + e.file);
            StringAssert.Contains(e.global, js, e.file + " 이 전역 " + e.global + " 을 정의해야 합니다.");
        }
    }

    [Test]
    public void WebGLBuildCopier_SubstitutesPerfFlags_ThroughJsStringEscaper_OnSameLine()
    {
        string source = ReadPackageFile("Editor/Package/WebGLBuildCopier.cs");

        bool found = false;
        foreach (string line in source.Split('\n'))
        {
            if (line.Contains(".Replace(\"%AIT_PERF_FLAGS%\""))
            {
                found = true;
                StringAssert.Contains("AITJsStringEscaper.EscapeSingleQuoted(", line,
                    "%AIT_PERF_FLAGS% 치환은 같은 줄에서 EscapeSingleQuoted 를 거쳐야 합니다.");
            }
        }
        Assert.IsTrue(found, "WebGLBuildCopier 에 %AIT_PERF_FLAGS% 치환이 없습니다.");
    }

    [Test]
    public void WebGLBuildCopier_RunsPatches_BeforePageCacheWarmManifestAndBrotli()
    {
        string source = ReadPackageFile("Editor/Package/WebGLBuildCopier.cs");

        int patch = source.IndexOf("ApplyBuildPatches(", System.StringComparison.Ordinal);
        int measure = source.IndexOf("MeasureDataRawSizeIfEnabled(", System.StringComparison.Ordinal);
        int json = source.IndexOf("AITPerfFlags.ToJson(", System.StringComparison.Ordinal);
        Assert.GreaterOrEqual(patch, 0, "ApplyBuildPatches 호출이 없습니다.");
        Assert.Less(patch, measure, "data 크기 측정은 패치 뒤여야 rename 된 이름을 씁니다.");
        Assert.Less(measure, json);

        foreach (string later in new[] { "AITBrotliCompressor", "BuildDataCacheName", "AITPageCache", "AITWarmManifest" })
        {
            int idx = source.IndexOf(later, System.StringComparison.Ordinal);
            if (idx < 0) continue; // 이름이 바뀐 경우를 이 테스트가 막지는 않는다 — 존재하는 것만 순서를 본다.
            Assert.Less(patch, idx, "패치는 " + later + " 보다 먼저 실행돼야 합니다.");
        }
    }

    private static string ReadIndexHtml()
    {
        return ReadPackageFile("WebGLTemplates/AITTemplate/index.html");
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
