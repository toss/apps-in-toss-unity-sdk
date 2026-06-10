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

    // === 게이팅 ===

    [Test]
    public void NullConfig_ReturnsEmpty()
    {
        string result = AITPageCacheEmitter.GenerateInterceptorScript(null, DataFile, FrameworkFile, WasmFile);
        Assert.AreEqual(string.Empty, result, "config==null 이면 빈 문자열(no-op)이어야 한다");
    }

    [Test]
    public void Disabled_ReturnsEmpty()
    {
        config.enablePageCache = false;
        string result = AITPageCacheEmitter.GenerateInterceptorScript(config, DataFile, FrameworkFile, WasmFile);
        Assert.AreEqual(string.Empty, result, "enablePageCache==false 이면 빈 문자열(byte-identical no-op)이어야 한다");
    }

    // === 활성 출력 ===

    [Test]
    public void Enabled_EmitsCacheNameLiteral()
    {
        config.enablePageCache = true;
        config.pageCacheName = "ait-page-cache";
        string result = AITPageCacheEmitter.GenerateInterceptorScript(config, DataFile, FrameworkFile, WasmFile);

        Assert.IsNotEmpty(result);
        StringAssert.Contains("ait-page-cache", result, "스니펫에 캐시명 리터럴이 박혀야 한다");
        StringAssert.Contains("<script>", result, "인라인 script 블록이어야 한다");
    }

    [Test]
    public void Enabled_AllowlistContainsBuildFiles()
    {
        config.enablePageCache = true;
        string result = AITPageCacheEmitter.GenerateInterceptorScript(config, DataFile, FrameworkFile, WasmFile);

        StringAssert.Contains("Build/" + DataFile, result, "allowlist 에 data 파일이 포함되어야 한다");
        StringAssert.Contains("Build/" + FrameworkFile, result, "allowlist 에 framework 파일이 포함되어야 한다");
        StringAssert.Contains("Build/" + WasmFile, result, "allowlist 에 wasm 파일이 포함되어야 한다");
    }

    [Test]
    public void Enabled_AllowlistExcludesLoader()
    {
        config.enablePageCache = true;
        // loaderFile 은 시그니처에 없으므로 어떤 경로로도 스니펫에 들어가면 안 됨(캐시 대상 외 계약).
        string result = AITPageCacheEmitter.GenerateInterceptorScript(config, DataFile, FrameworkFile, WasmFile);

        StringAssert.DoesNotContain(LoaderFile, result, "loader.js 는 캐시 대상이 아니므로 allowlist 에 없어야 한다");
        StringAssert.DoesNotContain(".loader.js", result, "loader 파일명 패턴이 스니펫에 없어야 한다");
    }

    [Test]
    public void Enabled_ContainsFeatureDetectGate()
    {
        config.enablePageCache = true;
        string result = AITPageCacheEmitter.GenerateInterceptorScript(config, DataFile, FrameworkFile, WasmFile);

        StringAssert.Contains("isSecureContext", result, "비보안 컨텍스트 feature-detect 가 있어야 한다");
        StringAssert.Contains("'caches' in window", result, "CacheStorage 지원 feature-detect 가 있어야 한다");
    }

    [Test]
    public void Enabled_ContainsBuildOriginGetGates()
    {
        config.enablePageCache = true;
        string result = AITPageCacheEmitter.GenerateInterceptorScript(config, DataFile, FrameworkFile, WasmFile);

        StringAssert.Contains("/Build/", result, "/Build/ 경로 게이트가 있어야 한다");
        StringAssert.Contains("GET", result, "GET 게이트가 있어야 한다");
        StringAssert.Contains("location.origin", result, "동일 오리진 게이트가 있어야 한다");
    }

    [Test]
    public void Enabled_CapturesPriorFetch()
    {
        config.enablePageCache = true;
        string result = AITPageCacheEmitter.GenerateInterceptorScript(config, DataFile, FrameworkFile, WasmFile);

        StringAssert.Contains("priorFetch", result, "priorFetch 캡처 패턴이 있어야 한다");
        StringAssert.Contains("window.fetch.bind(window)", result, "설치 시점 fetch 를 bind 로 캡처해야 한다");
    }

    [Test]
    public void Enabled_NonGetCheckInspectsRequestMethod()
    {
        config.enablePageCache = true;
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
        config.enablePageCache = true;
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
        config.enablePageCache = true;
        config.pageCacheName = "custom-bucket";
        string result = AITPageCacheEmitter.GenerateInterceptorScript(config, DataFile, FrameworkFile, WasmFile);

        StringAssert.Contains("custom-bucket", result, "사용자 지정 캐시명이 스니펫에 박혀야 한다");
    }

    [Test]
    public void Enabled_EmptyCacheName_FallsBackToDefault()
    {
        config.enablePageCache = true;
        config.pageCacheName = "";
        string result = AITPageCacheEmitter.GenerateInterceptorScript(config, DataFile, FrameworkFile, WasmFile);

        StringAssert.Contains("ait-page-cache", result, "빈 캐시명은 기본값 ait-page-cache 로 보정되어야 한다");
    }
}
