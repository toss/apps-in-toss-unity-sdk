// -----------------------------------------------------------------------
// AITPageCacheEmitterTests.cs - EditMode 페이지 캐시 인터셉터 스니펫 생성기 테스트
// Level 0: AITPageCacheEmitter.GenerateInterceptorScript 의 게이팅/allowlist/
//          플레이스홀더 안전/캐시명 보정 검증 (순수 문자열 생성기, 빌드 불필요)
// -----------------------------------------------------------------------

using NUnit.Framework;
using System.Text.RegularExpressions;
using UnityEngine;
using AppsInToss;
using AppsInToss.Editor.Package;

[TestFixture]
public class AITPageCacheEmitterTests
{
    private const string DataFile = "abc123.data";
    private const string FrameworkFile = "def456.framework.js";
    private const string WasmFile = "ghi789.wasm";
    private const string LoaderFile = "jkl012.loader.js";

    private AITEditorScriptObject config;

    [SetUp]
    public void Setup()
    {
        config = ScriptableObject.CreateInstance<AITEditorScriptObject>();
    }

    [TearDown]
    public void TearDown()
    {
        if (config != null)
        {
            Object.DestroyImmediate(config);
        }
    }

    // === 게이팅 (tri-state) ===

    [Test]
    public void NullConfig_ReturnsEmpty()
    {
        string result = AITPageCacheEmitter.GenerateInterceptorScript(null, DataFile, FrameworkFile, WasmFile);
        Assert.AreEqual(string.Empty, result, "config==null 이면 빈 문자열(no-op)이어야 한다");
    }

    [Test]
    public void PageCache_ExplicitDisabled_ReturnsEmpty()
    {
        config.pageCache = 0; // 명시적 비활성
        string result = AITPageCacheEmitter.GenerateInterceptorScript(config, DataFile, FrameworkFile, WasmFile);
        Assert.AreEqual(string.Empty, result, "pageCache==0(명시적 비활성) 이면 빈 문자열(byte-identical no-op)이어야 한다");
    }

    [Test]
    public void PageCache_Auto_DefaultIsEnabled()
    {
        config.pageCache = -1; // 자동 (기본값)
        string result = AITPageCacheEmitter.GenerateInterceptorScript(config, DataFile, FrameworkFile, WasmFile);
        Assert.IsNotEmpty(result, "pageCache==-1(자동) 이면 기본값 true 이므로 스니펫이 생성되어야 한다");
    }

    [Test]
    public void PageCache_ExplicitEnabled_EmitsScript()
    {
        config.pageCache = 1; // 명시적 활성
        string result = AITPageCacheEmitter.GenerateInterceptorScript(config, DataFile, FrameworkFile, WasmFile);
        Assert.IsNotEmpty(result, "pageCache==1(명시적 활성) 이면 스니펫이 생성되어야 한다");
    }

    // === 활성 출력 ===

    [Test]
    public void Enabled_EmitsCacheNameLiteral()
    {
        config.pageCache = 1;
        config.pageCacheName = "ait-page-cache";
        string result = AITPageCacheEmitter.GenerateInterceptorScript(config, DataFile, FrameworkFile, WasmFile);

        Assert.IsNotEmpty(result);
        StringAssert.Contains("ait-page-cache", result, "스니펫에 캐시명 리터럴이 박혀야 한다");
        StringAssert.Contains("<script>", result, "인라인 script 블록이어야 한다");
    }

    [Test]
    public void Enabled_AllowlistContainsBuildFiles()
    {
        config.pageCache = 1;
        string result = AITPageCacheEmitter.GenerateInterceptorScript(config, DataFile, FrameworkFile, WasmFile);

        StringAssert.Contains("Build/" + DataFile, result, "allowlist 에 data 파일이 포함되어야 한다");
        StringAssert.Contains("Build/" + FrameworkFile, result, "allowlist 에 framework 파일이 포함되어야 한다");
        StringAssert.Contains("Build/" + WasmFile, result, "allowlist 에 wasm 파일이 포함되어야 한다");
    }

    [Test]
    public void Enabled_AllowlistExcludesLoader()
    {
        config.pageCache = 1;
        // loaderFile 은 시그니처에 없으므로 어떤 경로로도 스니펫에 들어가면 안 됨(캐시 대상 외 계약).
        string result = AITPageCacheEmitter.GenerateInterceptorScript(config, DataFile, FrameworkFile, WasmFile);

        StringAssert.DoesNotContain(LoaderFile, result, "loader.js 는 캐시 대상이 아니므로 allowlist 에 없어야 한다");
        StringAssert.DoesNotContain(".loader.js", result, "loader 파일명 패턴이 스니펫에 없어야 한다");
    }

    [Test]
    public void Enabled_ContainsFeatureDetectGate()
    {
        config.pageCache = 1;
        string result = AITPageCacheEmitter.GenerateInterceptorScript(config, DataFile, FrameworkFile, WasmFile);

        StringAssert.Contains("isSecureContext", result, "비보안 컨텍스트 feature-detect 가 있어야 한다");
        StringAssert.Contains("'caches' in window", result, "CacheStorage 지원 feature-detect 가 있어야 한다");
    }

    [Test]
    public void Enabled_ContainsBuildOriginGetGates()
    {
        config.pageCache = 1;
        string result = AITPageCacheEmitter.GenerateInterceptorScript(config, DataFile, FrameworkFile, WasmFile);

        StringAssert.Contains("/Build/", result, "/Build/ 경로 게이트가 있어야 한다");
        StringAssert.Contains("GET", result, "GET 게이트가 있어야 한다");
        StringAssert.Contains("location.origin", result, "동일 오리진 게이트가 있어야 한다");
    }

    [Test]
    public void Enabled_CapturesPriorFetch()
    {
        config.pageCache = 1;
        string result = AITPageCacheEmitter.GenerateInterceptorScript(config, DataFile, FrameworkFile, WasmFile);

        StringAssert.Contains("priorFetch", result, "priorFetch 캡처 패턴이 있어야 한다");
        StringAssert.Contains("window.fetch.bind(window)", result, "설치 시점 fetch 를 bind 로 캡처해야 한다");
    }

    [Test]
    public void Enabled_NonGetCheckInspectsRequestMethod()
    {
        config.pageCache = 1;
        string result = AITPageCacheEmitter.GenerateInterceptorScript(config, DataFile, FrameworkFile, WasmFile);

        // 비-GET 판정이 init.method 뿐 아니라 Request 객체의 method 도 확인해야 한다.
        // fetch(new Request(url, {method:'POST'})) (init 미전달)을 GET 으로 오인하지 않도록 견고화.
        StringAssert.Contains("isNonGet", result, "비-GET 판정 헬퍼가 있어야 한다");
        StringAssert.Contains("resource.method", result,
            "Request 객체에 실린 method 도 비-GET 판정에 반영되어야 한다");
    }

    // === 플레이스홀더 안전 (ValidatePlaceholderSubstitution 회피) ===

    [Test]
    public void Enabled_HasNoUppercasePercentTokens()
    {
        config.pageCache = 1;
        string result = AITPageCacheEmitter.GenerateInterceptorScript(config, DataFile, FrameworkFile, WasmFile);

        // %FOO_BAR% 같은 대문자/숫자/언더스코어 퍼센트 토큰이 0개여야 빌드 치환 검증을 통과한다.
        var matches = Regex.Matches(result, "%[A-Z0-9_]+%");
        Assert.AreEqual(0, matches.Count,
            "스니펫에 %대문자_퍼센트% 토큰이 있으면 ValidatePlaceholderSubstitution 이 빌드를 실패시킨다");
    }

    // === 캐시명 오버라이드/보정 ===

    [Test]
    public void Enabled_CustomCacheName_IsEmbedded()
    {
        config.pageCache = 1;
        config.pageCacheName = "custom-bucket";
        string result = AITPageCacheEmitter.GenerateInterceptorScript(config, DataFile, FrameworkFile, WasmFile);

        StringAssert.Contains("custom-bucket", result, "사용자 지정 캐시명이 스니펫에 박혀야 한다");
    }

    [Test]
    public void Enabled_EmptyCacheName_WithAppName_DerivesCacheName()
    {
        config.pageCache = 1;
        config.pageCacheName = "";
        config.appName = "my-game";
        string result = AITPageCacheEmitter.GenerateInterceptorScript(config, DataFile, FrameworkFile, WasmFile);

        StringAssert.Contains("ait-page-cache-my-game", result,
            "빈 캐시명 + appName 'my-game' 이면 'ait-page-cache-my-game' 으로 자동 파생되어야 한다");
    }

    [Test]
    public void Enabled_EmptyCacheName_EmptyAppName_FallsBackToDefault()
    {
        config.pageCache = 1;
        config.pageCacheName = "";
        config.appName = "";
        string result = AITPageCacheEmitter.GenerateInterceptorScript(config, DataFile, FrameworkFile, WasmFile);

        StringAssert.Contains("ait-page-cache", result,
            "빈 캐시명 + 빈 appName 이면 기본값 ait-page-cache 로 폴백되어야 한다");
    }

    // === DeriveCacheName 순수 함수 단위 테스트 ===

    [Test]
    public void DeriveCacheName_AsciiIdentifier_ReturnsNormalizedSlug()
    {
        string result = AITPageCacheEmitter.DeriveCacheName("MyGame");
        Assert.AreEqual("ait-page-cache-mygame", result, "ASCII 식별자는 소문자 정규화 후 prefix 가 붙어야 한다");
    }

    [Test]
    public void DeriveCacheName_IdentifierWithSpecialChars_NormalizesToHyphen()
    {
        string result = AITPageCacheEmitter.DeriveCacheName("My Game!");
        Assert.AreEqual("ait-page-cache-my-game", result, "특수문자/공백은 하이픈으로 치환되어야 한다");
    }

    [Test]
    public void DeriveCacheName_NonAsciiIdentifier_ReturnsHashSlug()
    {
        string identifier = "내게임";
        string result = AITPageCacheEmitter.DeriveCacheName(identifier);

        Assert.IsNotNull(result, "비ASCII 식별자도 null 이 아닌 캐시명을 반환해야 한다");
        StringAssert.StartsWith("ait-page-cache-", result, "비ASCII 식별자도 prefix 가 붙어야 한다");
        // FNV-1a 32비트 해시는 8자리 16진수
        string slug = result.Substring("ait-page-cache-".Length);
        Assert.AreEqual(8, slug.Length, "비ASCII 식별자의 해시 슬러그는 8자리여야 한다");
        StringAssert.IsMatch("^[0-9a-f]{8}$", slug, "해시 슬러그는 16진수 소문자 8자리여야 한다");
    }

    [Test]
    public void DeriveCacheName_SameNonAsciiInput_ReturnsSameHash()
    {
        string identifier = "내게임";
        string result1 = AITPageCacheEmitter.DeriveCacheName(identifier);
        string result2 = AITPageCacheEmitter.DeriveCacheName(identifier);
        Assert.AreEqual(result1, result2, "동일 입력은 동일 해시를 반환해야 한다(결정론적)");
    }

    [Test]
    public void DeriveCacheName_EmptyString_ReturnsNull()
    {
        string result = AITPageCacheEmitter.DeriveCacheName("");
        Assert.IsNull(result, "빈 문자열 식별자는 null 을 반환해야 한다");
    }

    [Test]
    public void DeriveCacheName_NullString_ReturnsNull()
    {
        string result = AITPageCacheEmitter.DeriveCacheName(null);
        Assert.IsNull(result, "null 식별자는 null 을 반환해야 한다");
    }

    [Test]
    public void DeriveCacheName_DifferentInputs_ReturnDifferentHashes()
    {
        string result1 = AITPageCacheEmitter.DeriveCacheName("게임A");
        string result2 = AITPageCacheEmitter.DeriveCacheName("게임B");
        Assert.AreNotEqual(result1, result2, "서로 다른 식별자는 서로 다른 캐시명을 반환해야 한다");
    }

    // === ResolveCacheName 통합 테스트 ===

    [Test]
    public void ResolveCacheName_ExplicitName_ReturnsAsIs()
    {
        config.pageCacheName = "explicit-cache";
        config.appName = "some-app";
        string result = AITPageCacheEmitter.ResolveCacheName(config);
        Assert.AreEqual("explicit-cache", result, "명시적 캐시명이 있으면 그대로 반환해야 한다");
    }

    [Test]
    public void ResolveCacheName_EmptyName_WithAppName_Derives()
    {
        config.pageCacheName = "";
        config.appName = "cool-game";
        string result = AITPageCacheEmitter.ResolveCacheName(config);
        Assert.AreEqual("ait-page-cache-cool-game", result,
            "빈 캐시명 + appName 있으면 자동 파생해야 한다");
    }

    [Test]
    public void ResolveCacheName_EmptyName_EmptyAppName_FallsBack()
    {
        config.pageCacheName = "";
        config.appName = "";
        string result = AITPageCacheEmitter.ResolveCacheName(config);
        Assert.AreEqual(AITPageCacheEmitter.DefaultCacheName, result,
            "캐시명·appName 모두 비어 있으면 기본값으로 폴백해야 한다");
    }

    // === 네이티브 에셋 소스 레버 (nativeAssetSource tri-state) ===

    [Test]
    public void NativeAssetSource_Default_IsEnabled()
    {
        Assert.IsTrue(AITDefaultSettings.GetDefaultNativeAssetSource(),
            "네이티브 에셋 소스 기본값은 자동(true)이어야 한다");
    }

    [Test]
    public void NativeAuto_EmitsResolverGuardAndSignal()
    {
        config.pageCache = 1;
        config.nativeAssetSource = -1; // 자동 (기본 true)
        string result = AITPageCacheEmitter.GenerateInterceptorScript(config, DataFile, FrameworkFile, WasmFile);

        Assert.IsNotEmpty(result);
        StringAssert.Contains("var NATIVE_SOURCE = true", result,
            "자동(-1)이면 기본값 true 가 NATIVE_SOURCE 에 박혀야 한다");
        StringAssert.Contains("window.__aitNativeSourceEnabled", result,
            "호스트가 리졸버 주입 여부를 판단할 신호를 노출해야 한다");
        StringAssert.Contains("window.__aitResolveAsset", result,
            "호스트 리졸버 진입점(__aitResolveAsset) 분기가 있어야 한다");
        StringAssert.Contains("NATIVE_TIMEOUT_MS", result,
            "네이티브 응답 타임아웃 상수가 있어야 한다");
        StringAssert.Contains("clearTimeout", result,
            "타이머 누수 방지를 위해 clearTimeout 으로 타이머를 해제해야 한다");
    }

    [Test]
    public void NativeAuto_KeepsCacheFirstFallback()
    {
        config.pageCache = 1;
        config.nativeAssetSource = -1;
        string result = AITPageCacheEmitter.GenerateInterceptorScript(config, DataFile, FrameworkFile, WasmFile);

        // 네이티브 우선 분기가 추가되어도 cacheFirst 폴백 경로는 보존되어야 한다(native→CacheStorage→network).
        StringAssert.Contains("function cacheFirst", result,
            "cacheFirst 폴백 함수가 추출되어 있어야 한다");
        StringAssert.Contains("getCache", result,
            "CacheStorage 폴백 경로가 유지되어야 한다");
        StringAssert.Contains("native:", result,
            "네이티브 히트 통계 라벨(native:)이 있어야 한다");
    }

    [Test]
    public void NativeExplicitOff_DisablesSignalButKeepsCacheFirst()
    {
        config.pageCache = 1;
        config.nativeAssetSource = 0; // 명시적 비활성
        string result = AITPageCacheEmitter.GenerateInterceptorScript(config, DataFile, FrameworkFile, WasmFile);

        Assert.IsNotEmpty(result, "pageCache 가 ON 이면 인터셉터 자체는 여전히 생성되어야 한다");
        StringAssert.Contains("var NATIVE_SOURCE = false", result,
            "명시적 비활성(0)이면 NATIVE_SOURCE 가 false 여야 한다");
        StringAssert.Contains("getCache", result,
            "네이티브가 OFF 여도 cache-first 경로는 유지되어야 한다");
    }

    [Test]
    public void NativeOn_PageCacheOff_NoInterceptor()
    {
        config.pageCache = 0; // 인터셉터 없음 → 신호 주입 지점 자체가 없음
        config.nativeAssetSource = 1; // 명시적 활성이어도 무의미
        string result = AITPageCacheEmitter.GenerateInterceptorScript(config, DataFile, FrameworkFile, WasmFile);

        Assert.AreEqual(string.Empty, result,
            "pageCache==0 이면 인터셉터가 없으므로 nativeAssetSource 와 무관하게 빈 문자열이어야 한다(AND 게이트)");
    }

    [Test]
    public void NativeSignal_AfterSecureContextGuard()
    {
        config.pageCache = 1;
        config.nativeAssetSource = 1;
        string result = AITPageCacheEmitter.GenerateInterceptorScript(config, DataFile, FrameworkFile, WasmFile);

        // 신호는 보안/CacheStorage 가드(return) '이후'에 정의되어야 미지원 환경에서 미정의로 남는다(의도).
        int guardIdx = result.IndexOf("'caches' in window");
        int signalIdx = result.IndexOf("window.__aitNativeSourceEnabled");
        Assert.Greater(guardIdx, -1, "보안/CacheStorage 가드가 있어야 한다");
        Assert.Greater(signalIdx, guardIdx,
            "네이티브 신호는 미지원 가드 이후에 정의되어야 한다(미지원 환경 미정의 보장)");
    }
}
