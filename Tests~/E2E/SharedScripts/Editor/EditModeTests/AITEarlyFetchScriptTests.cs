// -----------------------------------------------------------------------
// AITEarlyFetchScriptTests.cs - Early Fetch 스크립트 생성 회귀 테스트
// Level 0: WebGLBuildCopier의 legacy/modern early-fetch 스크립트 구조 검증
//  · legacy(2021/2022): EARLY KICKOFF(콜드 선시작 + pending 합류) 포함, C#이 명시한
//    kickUrls(= data/wasm 만) 를 순회(loader/framework 는 <script src> 소비라 애초에 리스트에 없음),
//    기존 캐시/버퍼링 계약(legacy active/bufferedFetch/CACHE_NAME) 불변
//  · modern(6000.x): 기존 earlyFetchMap 구조 불변 (legacy 전용 킥오프 미혼입)
// -----------------------------------------------------------------------

using NUnit.Framework;
using AppsInToss.Editor.Package;

[TestFixture]
public class AITEarlyFetchScriptTests
{
    private const string UrlsJson =
        "[\"Build/aaaa.data.br\",\"Build/bbbb.wasm.br\",\"Build/cccc.framework.js.br\",\"Build/dddd.loader.js\"]";
    // C# GenerateEarlyFetchScript 가 넘기는 킥오프 대상: data/wasm 만(framework/loader 제외).
    private const string KickUrlsJson =
        "[\"Build/aaaa.data.br\",\"Build/bbbb.wasm.br\"]";
    private const string CacheName = "ait-unity-test-1-2-3";

    private static string Legacy() =>
        WebGLBuildCopier.GenerateEarlyFetchScriptLegacyCaching(UrlsJson, CacheName, KickUrlsJson);

    // ---------------- legacy (2021/2022) ----------------

    [Test]
    public void Legacy_ContainsEarlyKickoff_WithPendingJoin()
    {
        string js = Legacy();

        // 콜드 선시작: pendingEarly 맵 + isReload 게이트 + early-kick 로그
        StringAssert.Contains("var pendingEarly = {}", js);
        StringAssert.Contains("if (!isReload) {", js);
        StringAssert.Contains("early-kick", js);

        // 로더 fetch 합류: pending 재사용 + bodyUsed 가드 + originalFetch 폴백
        StringAssert.Contains("early-join", js);
        StringAssert.Contains("pendingEarly[url]", js);
        StringAssert.Contains("!r.bodyUsed", js);
    }

    [Test]
    public void Legacy_EarlyKickoff_IteratesExplicitKickUrls_NotSubstringHeuristic()
    {
        string js = Legacy();

        // 선시작은 C#이 명시한 kickUrls(data/wasm)를 순회해야 한다 — 절대 URL substring 매칭(휴리스틱)이 아님.
        // (host/path 에 '.data'/'.wasm' 이 우연히 들어가도 framework/loader 오탐 선시작이 없어야 함)
        StringAssert.Contains("var kickUrls = ", js);
        StringAssert.Contains("for (var ki = 0; ki < kickUrls.length; ki++)", js);
        StringAssert.Contains("new URL(kickUrls[ki], location.href).href", js);
        // 구 substring 휴리스틱(제거됨)이 되살아나면 실패
        StringAssert.DoesNotContain("url.indexOf('.data')", js);
        StringAssert.DoesNotContain("url.indexOf('.wasm')", js);
        StringAssert.DoesNotContain("for (var eu in knownSet) (function(url)", js);

        // kickUrls JSON 자체엔 data/wasm 만, framework/loader 는 없어야 한다
        StringAssert.Contains("Build/aaaa.data.br", js);
        StringAssert.Contains("Build/bbbb.wasm.br", js);
        int kickIdx = js.IndexOf("var kickUrls = ");
        Assert.GreaterOrEqual(kickIdx, 0, "'var kickUrls =' 앵커를 찾을 수 없음");
        int kickEnd = js.IndexOf(';', kickIdx);
        Assert.Greater(kickEnd, kickIdx, "kickUrls 선언 종료를 찾을 수 없음");
        string kickDecl = js.Substring(kickIdx, kickEnd - kickIdx);
        StringAssert.DoesNotContain(".framework.js", kickDecl);
        StringAssert.DoesNotContain(".loader.js", kickDecl);
    }

    [Test]
    public void Legacy_EarlyJoin_BypassesPendingWhenLoaderPassesAbortSignal()
    {
        string js = Legacy();

        // 로더가 취소 시그널을 넘기면 kickoff pending 재사용을 건너뛰고 실제 인자로 위임(취소 시맨틱 보존)
        StringAssert.Contains("if (init && init.signal) return originalFetch.apply(this, arguments);", js);
    }

    [Test]
    public void Legacy_EarlyKickoff_ChecksCacheBeforeKick()
    {
        string js = Legacy();

        // 재방문(비리로드) 캐시 HIT 에 네트워크 낭비가 없도록, 선시작 전에 캐시 match 를 확인해야 한다.
        // (kickoff 블록의 match → MISS 시에만 bufferedFetch)
        int kickIdx = js.IndexOf("var pendingEarly");
        Assert.GreaterOrEqual(kickIdx, 0, "'var pendingEarly' 앵커를 찾을 수 없음");
        int fetchIdx = js.IndexOf("window.fetch = function");
        Assert.Greater(fetchIdx, kickIdx, "'window.fetch = function' 앵커가 없거나 pendingEarly 보다 앞에 있음");
        string kickBlock = js.Substring(kickIdx, fetchIdx - kickIdx);
        StringAssert.Contains("c.match(url, { ignoreSearch: true })", kickBlock);
        StringAssert.Contains("bufferedFetch(url, MAX_TRIES)", kickBlock);
    }

    [Test]
    public void Legacy_EarlyKickoff_LowMemoryBranch_UsesBareFetch_NotBuffered()
    {
        string js = Legacy();

        // 저메모리(cacheOK=false) 분기는 OOM 방어를 위해 버퍼링(bufferedFetch) 없이 bare 스트리밍 fetch 여야 한다.
        // kickBlock 의 else 절만 격리해 검증 — else 가 실수로 bufferedFetch 로 바뀌는 회귀를 잡는다.
        int kickIdx = js.IndexOf("var pendingEarly");
        Assert.GreaterOrEqual(kickIdx, 0, "'var pendingEarly' 앵커를 찾을 수 없음");
        int fetchIdx = js.IndexOf("window.fetch = function");
        Assert.Greater(fetchIdx, kickIdx, "'window.fetch = function' 앵커를 찾을 수 없음");
        string kickBlock = js.Substring(kickIdx, fetchIdx - kickIdx);

        // 생성된 JS 는 '} else {' 형태(C# $@"" 의 }} 가 } 로 해제됨). else 절만 격리.
        int elseIdx = kickBlock.IndexOf("} else {");
        Assert.GreaterOrEqual(elseIdx, 0, "kickoff 의 else(저메모리) 분기를 찾을 수 없음");
        int elseEnd = kickBlock.IndexOf("pendingEarly[url] = p.catch", elseIdx);
        Assert.Greater(elseEnd, elseIdx, "else 분기 종료 지점을 찾을 수 없음");
        string elseBranch = kickBlock.Substring(elseIdx, elseEnd - elseIdx);
        StringAssert.Contains("originalFetch(url", elseBranch);
        StringAssert.DoesNotContain("bufferedFetch", elseBranch);
    }

    [Test]
    public void Legacy_ExistingContract_Unchanged()
    {
        string js = Legacy();

        // E2E(e2e-ce-serving.test.js)가 의존하는 로그 마커와 캐시/버퍼링 계약은 그대로여야 한다
        StringAssert.Contains("cache: legacy active", js);
        StringAssert.Contains("function bufferedFetch(url, left)", js);
        StringAssert.Contains("var CACHE_NAME = '" + CacheName + "'", js);
        StringAssert.Contains("__ait_skip_data_cache__", js);
        // CE 응답 길이 대조 생략 계약 (콜드 부트 이중 다운로드 방지)
        StringAssert.Contains("Content-Encoding", js);
    }

    [Test]
    public void Legacy_UrlsEmbedded()
    {
        string js = Legacy();
        // knownSet 대상(전체) 은 4개 모두 임베드
        StringAssert.Contains("Build/aaaa.data.br", js);
        StringAssert.Contains("Build/bbbb.wasm.br", js);
        StringAssert.Contains("Build/cccc.framework.js.br", js);
        StringAssert.Contains("Build/dddd.loader.js", js);
    }

    // ---------------- modern (6000.x) ----------------

    [Test]
    public void Modern_Structure_Unchanged()
    {
        // 디스패처 계약: modern 은 kickUrlsJson(data/wasm 만)을 받는다.
        string js = WebGLBuildCopier.GenerateEarlyFetchScriptModern(KickUrlsJson);

        // 6000.x 경로는 기존 earlyFetchMap 프리페치 구조 그대로 (legacy 전용 킥오프 미혼입)
        StringAssert.Contains("var earlyFetchMap = {}", js);
        StringAssert.Contains("if (isReload) return;", js);
        StringAssert.DoesNotContain("pendingEarly", js);
        StringAssert.DoesNotContain("early-kick", js);
        StringAssert.DoesNotContain("bufferedFetch", js);
    }

    [Test]
    public void Modern_PrefetchesDataAndWasmOnly()
    {
        // 6000.x 로더도 framework 을, index.html 은 loader 를 <script src> 로 소비하므로
        // prefetch 대상에 이 둘이 들어가면 응답이 소진되지 않고 이중 다운로드가 된다
        // (2026-07 베타 E2E CE 테스트에서 6000.x loader/framework 각 2회 다운로드로 실측 적발).
        // 디스패처는 modern 에 data/wasm 만 담긴 kickUrlsJson 을 넘겨야 한다.
        string js = WebGLBuildCopier.GenerateEarlyFetchScriptModern(KickUrlsJson);
        StringAssert.Contains("Build/aaaa.data.br", js);
        StringAssert.Contains("Build/bbbb.wasm.br", js);
        StringAssert.DoesNotContain(".framework.js", js);
        StringAssert.DoesNotContain(".loader.js", js);
    }
}
