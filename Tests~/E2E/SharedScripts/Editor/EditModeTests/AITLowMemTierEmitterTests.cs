// -----------------------------------------------------------------------
// AITLowMemTierEmitterTests.cs - EditMode 저메모리 tier / 지연 put 스니펫 생성 회귀 테스트
// Level 0: AITPageCacheEmitter / WebGLBuildCopier early-fetch 가 내보내는 JS 에
//          지연 put(only-if-cached, wasm 제외, 부팅 중 clone 없음)과 저메모리 tier 분기가 들어가는지 검증
// -----------------------------------------------------------------------

using NUnit.Framework;
using UnityEngine;
using AppsInToss;
using AppsInToss.Editor.Package;

[TestFixture]
public class AITLowMemTierEmitterTests
{
    private const string DataFile = "abc123.data";
    private const string FrameworkFile = "def456.framework.js";
    private const string WasmFile = "ghi789.wasm";
    private const string UrlsJson = "[\"Build/aaaa.data.br\",\"Build/bbbb.wasm.br\"]";
    private const string CacheName = "ait-unity-test-1-2-3";

    private AITEditorScriptObject config;

    [SetUp]
    public void Setup()
    {
        config = ScriptableObject.CreateInstance<AITEditorScriptObject>();
        config.pageCache = 1;
        config.nativeAssetSource = 0;
    }

    [TearDown]
    public void TearDown()
    {
        if (config != null) Object.DestroyImmediate(config);
    }

    private string Emit() => AITPageCacheEmitter.GenerateInterceptorScript(config, DataFile, FrameworkFile, WasmFile);

    [TestCase(-1)]
    [TestCase(0)]
    [TestCase(1)]
    public void PageCache_BakesDeferFlag(int flag)
    {
        config.pageCacheDeferredPut = flag;
        string js = Emit();
        StringAssert.Contains("var DEFER_FLAG_BAKED = " + flag + ";", js, "지연 put 플래그가 빌드 시점에 구워져야 한다");
    }

    [Test]
    public void PageCache_DeferredPut_ReadsBackViaOnlyIfCached_AndExcludesWasm()
    {
        string js = Emit();
        StringAssert.Contains("cache: 'only-if-cached'", js);
        StringAssert.Contains("mode: 'same-origin'", js);
        StringAssert.Contains("if (DEFERRED_PUT && WASM_ABS[_abs]) { continue; }", js, "지연 put 에서 wasm 은 put 대상이 아니다");
        StringAssert.Contains("function deferPutIfEnabled", js);
        StringAssert.Contains("function drainDeferred", js);
    }

    [Test]
    public void PageCache_CacheFirst_SkipsCloneWhenLowTierOrDeferred()
    {
        string js = Emit();
        StringAssert.Contains("resp.body !== undefined && !skipPutForLowMem(url) && !deferPutIfEnabled(url)", js,
            "put 용 clone 은 저메모리 tier/지연 put 이 아닐 때만 만들어져야 한다(부팅 중 clone 금지)");
    }

    [Test]
    public void PageCache_LowTier_UsesSharedPeek()
    {
        string js = Emit();
        StringAssert.Contains("__aitPeekLowTier", js);
        StringAssert.Contains("[AIT-PageCache] lowMemTier=", js);
    }

    [Test]
    public void PageCache_NoUnresolvedPlaceholders()
    {
        string js = Emit();
        StringAssert.DoesNotContain("%AIT_", js);
    }

    [Test]
    public void EarlyFetchModern_SkipsDataKickOnLowTier_KeepsPlaceholder()
    {
        string js = WebGLBuildCopier.GenerateEarlyFetchScriptModern(UrlsJson, "Build/aaaa.data.br");
        StringAssert.Contains("__aitPeekLowTier", js);
        StringAssert.Contains("data 선시작 생략", js);
        StringAssert.Contains("earlyFetchMap[absHref] = null", js, "래퍼가 복원되지 않도록 자리표시 항목을 남겨야 한다");
    }

    [Test]
    public void EarlyFetchModern_WithoutDataUrl_StillGenerates()
    {
        string js = WebGLBuildCopier.GenerateEarlyFetchScriptModern(UrlsJson);
        Assert.IsNotEmpty(js);
        StringAssert.DoesNotContain("%AIT_", js);
    }

    [Test]
    public void EarlyFetchLegacy_LowTierSkipsPutAndDataKick_DeferredStoresPlainBuffer()
    {
        string js = WebGLBuildCopier.GenerateEarlyFetchScriptLegacyCaching(UrlsJson, CacheName, UrlsJson, "Build/bbbb.wasm.br", "Build/aaaa.data.br");
        StringAssert.Contains("lowMemTier=", js);
        StringAssert.Contains("data 선시작 생략", js);
        StringAssert.Contains("only-if-cached", js);
        StringAssert.Contains("지연 put", js);
        StringAssert.DoesNotContain("{{", js, "보간 문자열의 중괄호 이스케이프가 새어 나오면 안 된다");
    }
}
