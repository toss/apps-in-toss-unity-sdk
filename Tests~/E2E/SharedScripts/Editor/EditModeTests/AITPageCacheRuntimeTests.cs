// -----------------------------------------------------------------------
// AITPageCacheRuntimeTests.cs - 페이지 캐시 인터셉터 스니펫 런타임 실행 검증
// Level 0: AITPageCacheEmitterTests 는 생성된 JS 의 토큰/구조만 텍스트 수준으로 검증해,
//  런타임 동작 회귀(캐시 히트 단락, 미스 시 비차단 put, 비-GET/비-allowlist 위임, 부팅
//  allowlist sweep, 비보안/백엔드 부재 시 무패치 통과, 설치 실패 시 진단 경고, native-first
//  분기)는 잡지 못한다. 602줄짜리 인라인 스크립트가 런타임 커버리지 0 인 채로 배포되면
//  로딩바 제거 블록이 SyntaxError 로 죽은 채 방치됐던 전례를 반복하게 된다.
//
//  이 테스트는 AITPageCacheEmitter.GenerateInterceptorScript 가 생성한 스니펫 본문을
//  Node 프로세스에서 실제로 실행해(window/location/caches/fetch/console mock 하네스,
//  네이티브 Response 로 ok/arrayBuffer/clone 시맨틱 실물 유지) 9개 런타임 계약을 검증한다.
//  계약 근거는 모두 Editor/Package/AITPageCacheEmitter.cs 의 JS 조각 주석에 있다.
//  하네스 구조는 AITEarlyFetchRuntimeTests 와 동일 패턴(Node 미탐지 시 Assert.Ignore,
//  ASSERT_FAIL/HARNESS_OK 프로토콜)을 따른다.
// -----------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using AppsInToss;
using AppsInToss.Editor;
using AppsInToss.Editor.Package;

[TestFixture]
public class AITPageCacheRuntimeTests
{
    // 하네스와 공유하는 픽스처 상수. 캐시명은 C# 쪽에서 config.pageCacheName 으로 명시해
    // 스니펫에 굽고, 하네스는 같은 값(CACHE)으로 스토어를 시드/검증한다.
    private const string CacheName = "ait-page-cache-test";
    private const string DataFile = "aaaa.data.br";
    private const string FrameworkFile = "cccc.framework.js.br";
    private const string WasmFile = "bbbb.wasm.br";

    // Node 하네스: 스니펫 본문을 (0, eval)로 실행하고 시나리오별 mock 글로벌을 세팅한 뒤
    // 계약을 assert 한다. 실패 시 stderr 에 'ASSERT_FAIL: ...' + 진단 로그를 쓰고 exit(1),
    // 성공 시 stdout 에 'HARNESS_OK' 를 쓰고 exit(0)한다.
    //
    // 절충: window/location/caches/fetch/console 은 mock 하되 Response 는 Node 네이티브를
    // 그대로 써서 ok/arrayBuffer/clone/body 시맨틱을 실물로 유지한다. indexedDB 는 모든
    // 시나리오에서 undefined 로 고정해(향후 Node 가 전역 indexedDB 를 추가해도) CacheStorage
    // 백엔드 경로가 결정적으로 선택되게 한다 — IDB 폴백 백엔드 자체는 이벤트 기반 API mock
    // 비용이 커서 이 하네스의 범위 밖이다(caches/IDB 는 getCache() 뒤에서 동일 cache-like
    // 인터페이스로 캡슐화되므로, 여기서 검증하는 호출부 계약은 백엔드와 무관하게 성립한다).
    private const string HarnessSource = @"import { readFileSync } from 'node:fs';

const scriptPath = process.argv[2];
const scenario = process.argv[3];
const body = readFileSync(scriptPath, 'utf8');

const ORIGIN = 'https://game.example.com';
const DATA = ORIGIN + '/Build/aaaa.data.br';
const LOADER = ORIGIN + '/Build/dddd.loader.js';
const OLD = ORIGIN + '/Build/old0ld.data.br';
const CACHE = 'ait-page-cache-test';

const logs = { log: [], warn: [], error: [] };
globalThis.console = {
  log: (...a) => { logs.log.push(a.join(' ')); },
  warn: (...a) => { logs.warn.push(a.join(' ')); },
  error: (...a) => { logs.error.push(a.join(' ')); },
};

const fetchCalls = [];
const fetchFactories = new Map();
async function mockFetch(resource, init) {
  const url = typeof resource === 'string' ? resource : resource.url;
  fetchCalls.push({ url, method: (init && init.method) || 'GET' });
  const factory = fetchFactories.get(url);
  if (!factory) throw new Error('mockFetch: no factory registered for ' + url);
  const spec = factory();
  return new Response(spec.body, { status: spec.status || 200, headers: spec.headers || {} });
}
function countCalls(url) { return fetchCalls.filter((c) => c.url === url).length; }

const cacheCalls = { open: [], match: [], put: [], keys: [], delete: [] };
const cacheStores = new Map();
function store(name) {
  if (!cacheStores.has(name)) cacheStores.set(name, new Map());
  return cacheStores.get(name);
}
function cacheHandle(name) {
  const s = store(name);
  return {
    match: async (url) => { cacheCalls.match.push(url); return s.get(url); },
    put: async (url, resp) => { cacheCalls.put.push(url); s.set(url, resp); },
    // 네이티브 Cache.keys() 와 동일하게 Request-like([{url}]) 배열을 돌려준다(sweep 이 .url 을 읽음).
    keys: async () => { cacheCalls.keys.push(name); return Array.from(s.keys()).map((u) => ({ url: u })); },
    // sweep 은 keys() 원소({url})를 그대로 delete 에 넘긴다.
    delete: async (entry) => {
      const key = entry && typeof entry === 'object' ? entry.url : entry;
      cacheCalls.delete.push(key);
      return s.delete(key);
    },
  };
}
const cachesMock = { open: async (name) => { cacheCalls.open.push(name); return cacheHandle(name); } };

function defineGlobal(name, value) {
  Object.defineProperty(globalThis, name, { value, writable: true, configurable: true, enumerable: true });
}

defineGlobal('window', globalThis);
defineGlobal('self', globalThis);
defineGlobal('location', { href: ORIGIN + '/', origin: ORIGIN });
defineGlobal('isSecureContext', true);
defineGlobal('caches', cachesMock);
defineGlobal('indexedDB', undefined);
defineGlobal('fetch', mockFetch);

async function settle(times = 6) { for (let i = 0; i < times; i++) await new Promise((r) => setTimeout(r, 0)); }

function fail(reason) {
  process.stderr.write('ASSERT_FAIL: ' + reason + '\n');
  process.stderr.write('LOGS.warn=' + JSON.stringify(logs.warn) + '\n');
  process.stderr.write('LOGS.error=' + JSON.stringify(logs.error) + '\n');
  process.stderr.write('FETCH_CALLS=' + JSON.stringify(fetchCalls) + '\n');
  process.stderr.write('CACHE_CALLS=' + JSON.stringify(cacheCalls) + '\n');
  process.exit(1);
}

function runScript() {
  try { (0, eval)(body); }
  catch (e) { fail('script threw during eval: ' + (e && e.stack ? e.stack : e)); }
}

function bodyBytes(n) { return new Uint8Array(n); }
function okFactory(n) {
  return () => ({ status: 200, headers: { 'Content-Type': 'application/octet-stream' }, body: bodyBytes(n) });
}

async function scenarioHitShortCircuit() {
  store(CACHE).set(DATA, new Response(bodyBytes(1000), { status: 200 }));
  runScript();
  await settle();
  if (window.fetch === mockFetch) fail('interceptor must patch window.fetch');
  const res = await window.fetch(DATA);
  if (!res || !res.ok) fail('cache hit response not ok');
  const buf = await res.arrayBuffer();
  if (buf.byteLength !== 1000) fail('hit body size mismatch: ' + buf.byteLength);
  if (countCalls(DATA) !== 0) fail('cache hit must short-circuit without network, got ' + countCalls(DATA));
  if (!window.__aitCacheStats.hits.includes(DATA)) fail('stats.hits must record ' + DATA);
  if (!store(CACHE).has(DATA)) fail('boot sweep must keep allowlisted entry');
}

async function scenarioMissThenPut() {
  fetchFactories.set(DATA, okFactory(1000));
  runScript();
  await settle();
  const res = await window.fetch(DATA);
  if (!res || !res.ok) fail('miss response not ok');
  const buf = await res.arrayBuffer();
  if (buf.byteLength !== 1000) fail('miss body size mismatch: ' + buf.byteLength);
  if (countCalls(DATA) !== 1) fail('miss must hit network exactly once, got ' + countCalls(DATA));
  await settle();
  if (!cacheCalls.put.includes(DATA)) fail('miss must trigger non-blocking cache.put');
  if (!window.__aitCacheStats.misses.includes(DATA)) fail('stats.misses must record ' + DATA);
  if (!window.__aitCacheStats.puts.includes(DATA)) fail('stats.puts must record ' + DATA);
  const dump = await window.__aitCacheDump();
  if (dump.length !== 1 || dump[0] !== DATA) fail('__aitCacheDump must list the stored key, got ' + JSON.stringify(dump));
}

async function scenarioNonGetDelegates() {
  fetchFactories.set(DATA, okFactory(1000));
  runScript();
  await settle();
  const res = await window.fetch(DATA, { method: 'POST' });
  if (!res || !res.ok) fail('POST response not ok');
  if (countCalls(DATA) !== 1) fail('POST must delegate to network, got ' + countCalls(DATA));
  if (cacheCalls.match.length !== 0) fail('POST must not consult cache.match, got ' + JSON.stringify(cacheCalls.match));
  if (cacheCalls.put.length !== 0) fail('POST response must not be cached');
}

async function scenarioNonAllowlistDelegates() {
  fetchFactories.set(LOADER, okFactory(500));
  runScript();
  await settle();
  const res = await window.fetch(LOADER);
  if (!res || !res.ok) fail('loader response not ok');
  if (countCalls(LOADER) !== 1) fail('non-allowlist /Build/ URL must delegate to network, got ' + countCalls(LOADER));
  if (cacheCalls.match.length !== 0) fail('non-allowlist must not consult cache.match');
  if (cacheCalls.put.length !== 0) fail('non-allowlist response must not be cached');
}

async function scenarioBootSweep() {
  store(CACHE).set(DATA, new Response(bodyBytes(1000), { status: 200 }));
  store(CACHE).set(OLD, new Response(bodyBytes(999), { status: 200 }));
  runScript();
  await settle();
  if (store(CACHE).has(OLD)) fail('boot sweep must delete the non-allowlist old-hash entry');
  if (!store(CACHE).has(DATA)) fail('boot sweep must keep the allowlisted entry');
  if (!cacheCalls.delete.includes(OLD)) fail('sweep must call delete on the old entry');
}

async function scenarioInsecureNoPatch() {
  defineGlobal('isSecureContext', false);
  runScript();
  await settle();
  if (window.fetch !== mockFetch) fail('insecure context must leave window.fetch untouched');
  if (cacheCalls.open.length !== 0) fail('insecure context must not open any cache');
}

async function scenarioNoBackendNoPatch() {
  delete globalThis.caches;
  runScript();
  await settle();
  if (window.fetch !== mockFetch) fail('no-backend environment must leave window.fetch untouched');
}

async function scenarioInstallErrorWarns() {
  defineGlobal('fetch', undefined);
  runScript();
  await settle();
  if (window.fetch !== undefined) fail('failed install must not leave a partial fetch patch');
  if (!logs.warn.some((s) => s.indexOf('[AIT PageCache]') >= 0)) {
    fail('install failure must emit a diagnosable console.warn, warns=' + JSON.stringify(logs.warn));
  }
}

async function scenarioNativeFirstServes() {
  let resolverCalls = 0;
  window.__aitResolveAsset = async () => { resolverCalls++; return new Response(bodyBytes(777), { status: 200 }); };
  runScript();
  await settle();
  if (window.__aitNativeSourceEnabled !== true) fail('NATIVE_SOURCE lever must expose the enabled signal');
  const res = await window.fetch(DATA);
  if (!res || !res.ok) fail('native response not ok');
  const buf = await res.arrayBuffer();
  if (buf.byteLength !== 777) fail('native response must be served as-is, got ' + buf.byteLength);
  if (resolverCalls !== 1) fail('resolver must be called exactly once, got ' + resolverCalls);
  if (countCalls(DATA) !== 0) fail('native hit must not touch network');
  if (cacheCalls.put.length !== 0) fail('native response must not be cache.put (store duplication guard)');
  if (!window.__aitCacheStats.hits.some((h) => h === 'native:' + DATA)) fail('stats.hits must record the native hit');
}

async function main() {
  switch (scenario) {
    case 'hit_short_circuit': await scenarioHitShortCircuit(); break;
    case 'miss_then_put': await scenarioMissThenPut(); break;
    case 'non_get_delegates': await scenarioNonGetDelegates(); break;
    case 'non_allowlist_delegates': await scenarioNonAllowlistDelegates(); break;
    case 'boot_sweep': await scenarioBootSweep(); break;
    case 'insecure_no_patch': await scenarioInsecureNoPatch(); break;
    case 'no_backend_no_patch': await scenarioNoBackendNoPatch(); break;
    case 'install_error_warns': await scenarioInstallErrorWarns(); break;
    case 'native_first_serves': await scenarioNativeFirstServes(); break;
    default: fail('unknown scenario ' + scenario); return;
  }
  process.stdout.write('HARNESS_OK\n');
  process.exit(0);
}
main().catch((e) => fail('uncaught: ' + (e && e.stack ? e.stack : e)));
";

    /// <summary>
    /// 실제 emitter 로 스니펫을 생성해 순수 JS 본문만 벗겨낸다.
    /// nativeOn 으로 NATIVE_SOURCE 보간을 제어한다(기본 시나리오는 native 분기 비활성 고정 —
    /// 기본값 변화에 테스트가 흔들리지 않도록 tri-state 를 명시값으로 굽는다).
    /// </summary>
    private static string GenerateScriptBody(bool nativeOn)
    {
        var config = ScriptableObject.CreateInstance<AITEditorScriptObject>();
        try
        {
            config.pageCache = 1;
            config.pageCacheName = CacheName;
            config.nativeAssetSource = nativeOn ? 1 : 0;
            string wrapped = AITPageCacheEmitter.GenerateInterceptorScript(config, DataFile, FrameworkFile, WasmFile);
            return ExtractScriptBody(wrapped);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(config);
        }
    }

    // 생성 스니펫은 '<script> ... </script>' 로 래핑되어 있다 — Node 에 넘길 순수 JS 본문만 벗겨낸다.
    private static string ExtractScriptBody(string wrapped)
    {
        int tagEnd = wrapped.IndexOf('>');
        Assert.GreaterOrEqual(tagEnd, 0, "<script> 시작 태그를 찾을 수 없습니다.");
        int start = tagEnd + 1;
        int end = wrapped.LastIndexOf("</script>", StringComparison.Ordinal);
        Assert.Greater(end, start, "</script> 종료 태그를 찾을 수 없거나 시작 태그보다 앞에 있습니다.");
        return wrapped.Substring(start, end - start);
    }

    // 시나리오 하나를 임시 디렉토리에 스크립트/하네스를 써서 Node 로 실행하고 계약 통과를 확인한다.
    private static void RunScenario(string scenarioName, bool nativeOn = false)
    {
        string nodePath = AITPackageManagerHelper.FindExecutable("node", verbose: false);
        if (string.IsNullOrEmpty(nodePath))
        {
            Assert.Ignore("Node 실행 파일 없음 — 런타임 실행 테스트 건너뜀");
        }

        string scriptBody = GenerateScriptBody(nativeOn);

        string tempDir = Path.Combine(Path.GetTempPath(), "ait-page-cache-runtime-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string scriptPath = Path.Combine(tempDir, "script.js");
        string harnessPath = Path.Combine(tempDir, "harness.mjs");

        try
        {
            File.WriteAllText(scriptPath, scriptBody);
            File.WriteAllText(harnessPath, HarnessSource);

            var startInfo = new ProcessStartInfo
            {
                FileName = nodePath,
                Arguments = $"\"{harnessPath}\" \"{scriptPath}\" {scenarioName}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            AITProcessExecutor.Result result = AITProcessExecutor.Run(startInfo, 60000);

            Assert.IsFalse(result.TimedOut,
                $"시나리오 '{scenarioName}' 하네스가 60초 내 종료되지 않았습니다.\n--- STDOUT ---\n{result.StdOut}\n--- STDERR ---\n{result.StdErr}");
            Assert.AreEqual(0, result.ExitCode,
                $"시나리오 '{scenarioName}' 하네스가 실패했습니다(계약 위반).\n--- STDOUT ---\n{result.StdOut}\n--- STDERR ---\n{result.StdErr}");
            StringAssert.Contains("HARNESS_OK", result.StdOut,
                $"시나리오 '{scenarioName}' 하네스가 성공 마커를 출력하지 않았습니다.\n--- STDOUT ---\n{result.StdOut}\n--- STDERR ---\n{result.StdErr}");
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); }
            catch { /* best-effort 정리 — 실패해도 테스트 결과에 영향 없음 */ }
        }
    }

    [Test]
    public void CacheHit_ShortCircuits_WithoutNetwork()
    {
        // 재방문 히트: 사전 채워진 캐시 엔트리를 그대로 서빙하고 네트워크를 전혀 건드리지 않아야
        // 한다(transferSize 0 단락 — 이 기능의 존재 이유). 부팅 sweep 이 allowlist 엔트리를
        // 지우지 않는 것도 함께 확인한다.
        RunScenario("hit_short_circuit");
    }

    [Test]
    public void CacheMiss_FetchesOnce_ThenPutsNonBlocking()
    {
        // 콜드 미스: 네트워크 1회 → 응답은 가공 없이 반환, put 은 비차단으로 뒤따라야 한다.
        // 통계 훅(misses/puts)과 __aitCacheDump 헬퍼 동작도 함께 검증한다.
        RunScenario("miss_then_put");
    }

    [Test]
    public void NonGetRequest_DelegatesToNetwork_WithoutCacheTouch()
    {
        // POST 등 비-GET 은 cache.match 를 거치지 않고 원래 fetch 로 위임해야 한다
        // (GET 캐시 엔트리로 POST 에 응답하는 사고 방지 — isNonGet 계약).
        RunScenario("non_get_delegates");
    }

    [Test]
    public void NonAllowlistBuildUrl_DelegatesToNetwork()
    {
        // /Build/ 경로라도 allowlist(현재 빌드의 data/framework/wasm) 밖이면 — 대표적으로
        // loader.js — 캐시를 건드리지 않고 위임해야 한다(early-fetch 의 HTTP 캐시 워밍 목적을
        // 해치지 않도록. isCacheable 계약).
        RunScenario("non_allowlist_delegates");
    }

    [Test]
    public void BootSweep_DeletesStaleEntries_KeepsAllowlisted()
    {
        // 설치 직후 1회 비차단 sweep: allowlist 에 없는 옛 해시 엔트리는 삭제, 현재 빌드
        // 엔트리는 보존해야 한다(콘텐츠 버전 정합).
        RunScenario("boot_sweep");
    }

    [Test]
    public void InsecureContext_LeavesFetchUntouched()
    {
        // 비보안 컨텍스트: window.fetch 를 패치하지 않고 원래 로드로 무해 통과해야 한다
        // (설치 가드 최우선 계약).
        RunScenario("insecure_no_patch");
    }

    [Test]
    public void NoBackend_LeavesFetchUntouched()
    {
        // CacheStorage/IndexedDB 둘 다 없는 환경(구형 브라우저): 전체 no-op 이어야 한다.
        RunScenario("no_backend_no_patch");
    }

    [Test]
    public void InstallFailure_EmitsDiagnosableWarn_WithoutBreakingBoot()
    {
        // 설치 중 예외(여기서는 window.fetch 가 함수가 아닌 환경으로 유발): 부팅을 막지 않되
        // 완전 침묵하면 안 된다 — 페이지 캐시가 꺼진 채 아무도 모르는 상태(로딩바 제거 블록이
        // 같은 방식으로 죽어 있던 전례)를 막기 위해 최상위 catch 가 console.warn 을 남겨야 한다.
        RunScenario("install_error_warns");
    }

    [Test]
    public void NativeFirst_ServesResolverResponse_WithoutCachePut()
    {
        // native-first 분기(레버 ON + 리졸버 주입): 네이티브 응답을 그대로 서빙하고
        // cache.put 하지 않아야 한다(스토어 이중화 방지 계약). 신호(__aitNativeSourceEnabled)
        // 노출과 통계 기록('native:' 접두 히트)도 함께 확인한다.
        RunScenario("native_first_serves", nativeOn: true);
    }
}
