// -----------------------------------------------------------------------
// AITEarlyFetchRuntimeTests.cs - Early Fetch(legacy) 킥오프 스크립트 런타임 실행 검증
// Level 0: AITEarlyFetchScriptTests는 생성된 JS의 토큰 존재만 StringAssert로 검증해,
//  런타임 동작 회귀(로더 fetch의 pending 합류, bodyUsed 응답 재사용 방지 폴백, 저메모리
//  분기의 실제 fetch 선택, init.signal 우회, reload 게이트, SKIP_KEY 우회, short-read
//  재시도, Content-Encoding 길이 대조 생략)는 잡지 못한다(TODO.md P3 항목: "레거시
//  early-fetch 킥오프 런타임 실행 기반 테스트 보강").
//
//  이 테스트는 WebGLBuildCopier.GenerateEarlyFetchScriptLegacyCaching이 생성한 스크립트
//  본문을 Node 프로세스에서 실제로 실행해(fetch/caches/sessionStorage/performance/navigator
//  mock 하네스, 네이티브 Response로 bodyUsed/arrayBuffer/headers 시맨틱 실물 유지), 10개
//  런타임 계약을 검증한다. 계약 근거는 모두 Editor/Package/WebGLBuildCopier.cs 의
//  GenerateEarlyFetchScriptLegacyCaching 본문 주석(697~872행)에 있다.
//
//  Node 미탐지 환경(오프라인 batchmode 등)에서는 Assert.Ignore로 건너뛴다 — 결정적 토큰
//  검증은 AITEarlyFetchScriptTests가 항상 담당하므로 회귀 방어 공백은 없다.
// -----------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;
using AppsInToss.Editor;
using AppsInToss.Editor.Package;

[TestFixture]
public class AITEarlyFetchRuntimeTests
{
    // AITEarlyFetchScriptTests 와 동일한 입력(URL 상수/캐시명)을 재사용해 두 테스트가
    // 같은 계약 표면을 검증하도록 맞춘다.
    private const string UrlsJson =
        "[\"Build/aaaa.data.br\",\"Build/bbbb.wasm.br\",\"Build/cccc.framework.js.br\",\"Build/dddd.loader.js\"]";
    private const string KickUrlsJson =
        "[\"Build/aaaa.data.br\",\"Build/bbbb.wasm.br\"]";
    private const string CacheName = "ait-unity-test-1-2-3";

    // Node 하네스: 스크립트 본문을 (0, eval)로 실행하고 시나리오별 mock 글로벌을 세팅한 뒤
    // 계약을 assert한다. 실패 시 stderr에 'ASSERT_FAIL: ...' + 진단 로그를 쓰고 exit(1),
    // 성공 시 stdout에 'HARNESS_OK'를 쓰고 exit(0)한다.
    //
    // 절충: caches/fetch/sessionStorage/navigator/location/performance/console 은 모두
    // 하네스가 mock한다(실제 Cache Storage/네트워크 없이 계약만 검증). fetch 응답은 Node
    // 네이티브 Response 로 만들어 bodyUsed/arrayBuffer/headers.get 시맨틱을 실물로 유지한다
    // (시나리오 2/3의 bodyUsed 판별이 여기 의존). Node 24 는 전역 navigator/performance/
    // caches/fetch 가 getter-only 라 Object.defineProperty 로 재정의한다.
    private const string HarnessSource = @"import { readFileSync } from 'node:fs';

const scriptPath = process.argv[2];
const scenario = process.argv[3];
const body = readFileSync(scriptPath, 'utf8');

const DATA = 'https://game.example.com/Build/aaaa.data.br';
const WASM = 'https://game.example.com/Build/bbbb.wasm.br';
const SKIP_KEY = '__ait_skip_data_cache__';

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
  const callIndexForUrl = fetchCalls.filter((c) => c.url === url).length;
  const record = { url, init, method: init && init.method };
  fetchCalls.push(record);
  const factory = fetchFactories.get(url);
  if (!factory) throw new Error('mockFetch: no factory registered for ' + url);
  const spec = factory(callIndexForUrl);
  const res = new Response(spec.body, { status: spec.status || 200, headers: spec.headers || {} });
  record.response = res;
  return res;
}
function lastResponse(url) {
  const arr = fetchCalls.filter((c) => c.url === url);
  return arr.length ? arr[arr.length - 1].response : undefined;
}
function countCalls(url) {
  return fetchCalls.filter((c) => c.url === url).length;
}

const cacheCalls = { open: [], match: [], put: [], keys: [], delete: [] };
const cacheStores = new Map();
function cacheHandle(name) {
  if (!cacheStores.has(name)) cacheStores.set(name, new Map());
  const store = cacheStores.get(name);
  return {
    match: async (url, opts) => {
      cacheCalls.match.push({ name, url, opts });
      // 실제 Cache Storage의 ignoreSearch:true 시맨틱을 반영: 쿼리스트링을 제거하고 매칭한다.
      // (opts를 기록만 하고 무시하면 소스가 ignoreSearch를 실수로 빠뜨려도 이 mock은 여전히
      // 정확 일치로 통과시켜 회귀를 잡지 못한다 — 실제 매칭 로직에 반영해야 한다.)
      if (opts && opts.ignoreSearch) {
        const baseUrl = url.split('?')[0];
        for (const [storedUrl, storedResp] of store) {
          if (storedUrl.split('?')[0] === baseUrl) return storedResp;
        }
        return undefined;
      }
      return store.get(url);
    },
    put: async (url, response) => { cacheCalls.put.push({ name, url }); store.set(url, response); },
  };
}
const cachesMock = {
  open: async (name) => { cacheCalls.open.push(name); return cacheHandle(name); },
  keys: async () => { cacheCalls.keys.push(true); return Array.from(cacheStores.keys()); },
  delete: async (name) => { cacheCalls.delete.push(name); return cacheStores.delete(name); },
};

const sessionMap = new Map();
const sessionStorageMock = {
  getItem: (k) => (sessionMap.has(k) ? sessionMap.get(k) : null),
  setItem: (k, v) => { sessionMap.set(k, String(v)); },
  removeItem: (k) => { sessionMap.delete(k); },
};

let navType = 'navigate';
const performanceMock = { getEntriesByType: (t) => (t === 'navigation' ? [{ type: navType }] : []) };

let deviceMemory;
const navigatorMock = { get deviceMemory() { return deviceMemory; } };

function defineGlobal(name, value) {
  Object.defineProperty(globalThis, name, { value, writable: true, configurable: true, enumerable: true });
}

defineGlobal('window', globalThis);
defineGlobal('self', globalThis);
defineGlobal('location', { href: 'https://game.example.com/' });
defineGlobal('navigator', navigatorMock);
defineGlobal('performance', performanceMock);
defineGlobal('sessionStorage', sessionStorageMock);
defineGlobal('caches', cachesMock);
defineGlobal('fetch', mockFetch);

async function settle(times = 5) {
  for (let i = 0; i < times; i++) await new Promise((r) => setTimeout(r, 0));
}

function fail(reason) {
  process.stderr.write('ASSERT_FAIL: ' + reason + '\n');
  process.stderr.write('LOGS.log=' + JSON.stringify(logs.log) + '\n');
  process.stderr.write('LOGS.warn=' + JSON.stringify(logs.warn) + '\n');
  process.stderr.write('LOGS.error=' + JSON.stringify(logs.error) + '\n');
  process.stderr.write('FETCH_CALLS=' + JSON.stringify(fetchCalls.map((c) => ({ url: c.url, method: c.method }))) + '\n');
  process.stderr.write('CACHE_CALLS=' + JSON.stringify({ open: cacheCalls.open, match: cacheCalls.match.map((m) => m.url), put: cacheCalls.put.map((p) => p.url) }) + '\n');
  process.exit(1);
}

function hasLog(arr, needle) {
  return arr.some((s) => s.indexOf(needle) >= 0);
}

function runScript() {
  try {
    (0, eval)(body);
  } catch (e) {
    fail('script threw during eval: ' + (e && e.stack ? e.stack : e));
  }
}

function stdBody(bytes) { return new Uint8Array(bytes); }
function dataFactoryOK(bytes) {
  return () => ({ status: 200, headers: { 'Content-Type': 'application/octet-stream', 'Content-Length': String(bytes) }, body: stdBody(bytes) });
}
function wasmFactoryOK(bytes) {
  return () => ({ status: 200, headers: { 'Content-Type': 'application/wasm', 'Content-Length': String(bytes) }, body: stdBody(bytes) });
}

async function scenarioKickAndJoin() {
  deviceMemory = undefined;
  navType = 'navigate';
  globalThis.caches = cachesMock;
  fetchFactories.set(DATA, dataFactoryOK(1000));
  fetchFactories.set(WASM, wasmFactoryOK(2000));

  runScript();
  await settle();

  if (countCalls(DATA) !== 1) fail('expected DATA fetched once after kickoff, got ' + countCalls(DATA));
  if (countCalls(WASM) !== 1) fail('expected WASM fetched once after kickoff, got ' + countCalls(WASM));
  const kickLogCount = logs.log.filter((s) => s.indexOf('early-kick') >= 0).length;
  if (kickLogCount !== 2) fail('expected 2 early-kick logs, got ' + kickLogCount);

  const res = await window.fetch(DATA);
  if (!res || !res.ok) fail('joined fetch not ok');
  const buf = await res.arrayBuffer();
  if (buf.byteLength !== 1000) fail('joined response body size mismatch: ' + buf.byteLength);
  if (countCalls(DATA) !== 1) fail('expected no extra DATA network call after join, got ' + countCalls(DATA));
  if (!hasLog(logs.log, 'early-join')) fail('expected early-join log');

  const kickMatch = cacheCalls.match.find((c) => c.url === DATA);
  if (!kickMatch || !kickMatch.opts || kickMatch.opts.ignoreSearch !== true) {
    fail('kickoff cache.match must be called with { ignoreSearch: true }');
  }
}

async function scenarioBodyUsedFallback() {
  deviceMemory = undefined;
  navType = 'navigate';
  globalThis.caches = undefined;
  fetchFactories.set(DATA, dataFactoryOK(1000));
  fetchFactories.set(WASM, wasmFactoryOK(2000));

  runScript();
  await settle();

  if (countCalls(DATA) !== 1) fail('kickoff should bare-fetch DATA once, got ' + countCalls(DATA));
  if (cacheCalls.open.length !== 0) fail('no-cache scenario must not call caches.open');

  const kicked = lastResponse(DATA);
  await kicked.arrayBuffer();

  const res = await window.fetch(DATA);
  if (countCalls(DATA) !== 2) fail('bodyUsed pending must trigger originalFetch fallback re-call, got ' + countCalls(DATA));
  if (!res || !res.ok) fail('fallback response not ok');
}

async function scenarioLowMemory() {
  deviceMemory = 2;
  navType = 'navigate';
  globalThis.caches = cachesMock;
  fetchFactories.set(DATA, dataFactoryOK(1000));
  fetchFactories.set(WASM, wasmFactoryOK(2000));

  runScript();
  await settle();

  if (countCalls(DATA) !== 1) fail('expected DATA fetched once after kickoff, got ' + countCalls(DATA));
  if (cacheCalls.open.length !== 0) fail('low-memory kickoff must not touch caches.open, got ' + cacheCalls.open.length);
  if (cacheCalls.put.length !== 0) fail('low-memory kickoff must not buffer/store, put calls=' + cacheCalls.put.length);
  const kicked = lastResponse(DATA);
  if (kicked.bodyUsed) fail('low-memory kickoff must not buffer the body (bufferedFetch must not run)');

  const res = await window.fetch(DATA);
  if (!res || !res.ok) fail('join response not ok');
  if (countCalls(DATA) !== 1) fail('join should reuse pending without re-fetch, got ' + countCalls(DATA));
}

async function scenarioSignalBypass() {
  deviceMemory = undefined;
  navType = 'navigate';
  globalThis.caches = cachesMock;
  fetchFactories.set(DATA, dataFactoryOK(1000));
  fetchFactories.set(WASM, wasmFactoryOK(2000));

  runScript();
  await settle();

  if (countCalls(DATA) !== 1) fail('expected DATA fetched once after kickoff, got ' + countCalls(DATA));

  const ac = new AbortController();
  const res = await window.fetch(DATA, { signal: ac.signal });
  if (hasLog(logs.log, 'early-join')) fail('signal bypass must not log early-join');
  if (countCalls(DATA) !== 2) fail('signal bypass must delegate to originalFetch, got ' + countCalls(DATA));
  if (!res || !res.ok) fail('signal-bypass response not ok');

  const matchBefore = cacheCalls.match.filter((c) => c.url === DATA).length;
  const res2 = await window.fetch(DATA);
  await settle(2);
  const matchAfter = cacheCalls.match.filter((c) => c.url === DATA).length;
  if (matchAfter <= matchBefore) fail('subsequent join-less fetch should go through cache.match path again');
  if (!res2 || !res2.ok) fail('subsequent fetch response not ok');
}

async function scenarioReloadNoKick() {
  deviceMemory = undefined;
  navType = 'reload';
  globalThis.caches = cachesMock;
  fetchFactories.set(DATA, dataFactoryOK(1000));
  fetchFactories.set(WASM, wasmFactoryOK(2000));

  runScript();
  await settle();

  if (countCalls(DATA) !== 0 || countCalls(WASM) !== 0) fail('reload must not trigger any kickoff network calls');
  if (hasLog(logs.log, 'early-kick')) fail('reload must not log early-kick');

  const res = await window.fetch(DATA);
  if (!res || !res.ok) fail('reload fetch via cache path not ok');
  if (countCalls(DATA) !== 1) fail('reload fetch should hit network exactly once via MISS->bufferedFetch, got ' + countCalls(DATA));

  const reloadMatch = cacheCalls.match.find((c) => c.url === DATA);
  if (!reloadMatch || !reloadMatch.opts || reloadMatch.opts.ignoreSearch !== true) {
    fail('reload fetch cache.match must be called with { ignoreSearch: true }');
  }
}

// isReload=true + deviceMemory<4 조합: 킥오프 루프 자체가 스킵되므로(isReload) pendingEarly가
// 비고, window.fetch 오버라이드의 '!cacheOK -> bare originalFetch' 분기(850~851행)가 유일한
// 실행 경로가 된다. 이 분기가 없으면 caches.open을 거쳐 저메모리 OOM 방어가 무력화된다.
async function scenarioReloadLowMemory() {
  deviceMemory = 2;
  navType = 'reload';
  globalThis.caches = cachesMock;
  fetchFactories.set(DATA, dataFactoryOK(1000));
  fetchFactories.set(WASM, wasmFactoryOK(2000));

  runScript();
  await settle();

  if (countCalls(DATA) !== 0 || countCalls(WASM) !== 0) fail('reload+low-memory must not trigger any kickoff network calls');

  const res = await window.fetch(DATA);
  if (!res || !res.ok) fail('reload+low-memory fetch not ok');
  if (cacheCalls.open.length !== 0) fail('reload+low-memory fetch must not touch caches.open, got ' + cacheCalls.open.length);
  if (countCalls(DATA) !== 1) fail('reload+low-memory fetch should hit network exactly once (bare passthrough), got ' + countCalls(DATA));
  if (res.bodyUsed) fail('reload+low-memory fetch must not be buffered (bare originalFetch passthrough expected)');
}

// isReload=true + SKIP_KEY 조합: 킥오프 루프가 스킵되므로(isReload) pendingEarly가 비고,
// window.fetch 오버라이드의 skipCacheOnce 분기(854행 부근, cache.match 우회 후 bufferedFetch
// 직행)가 유일한 실행 경로가 된다. 이 분기가 무력화되면 워치독 복구 reload에서 오염된 캐시를
// 그대로 match해 서빙할 수 있다(self-amplification 방어 실효).
async function scenarioReloadSkipKey() {
  deviceMemory = undefined;
  navType = 'reload';
  globalThis.caches = cachesMock;
  sessionStorageMock.setItem(SKIP_KEY, '1');
  fetchFactories.set(DATA, dataFactoryOK(1000));
  fetchFactories.set(WASM, wasmFactoryOK(2000));

  runScript();
  await settle();

  if (countCalls(DATA) !== 0 || countCalls(WASM) !== 0) fail('reload+skip-key must not trigger any kickoff network calls');
  if (sessionStorageMock.getItem(SKIP_KEY) !== null) fail('SKIP_KEY must be consumed(removed) after script init even on reload');

  const res = await window.fetch(DATA);
  const matchesForData = cacheCalls.match.filter((c) => c.url === DATA).length;
  if (matchesForData !== 0) fail('skip-key reload fetch must bypass cache.match, got ' + matchesForData);
  if (!res || !res.ok) fail('skip-key reload fetch not ok');
  if (countCalls(DATA) !== 1) fail('skip-key reload fetch should hit network exactly once, got ' + countCalls(DATA));
}

async function scenarioSkipKeyBypass() {
  deviceMemory = undefined;
  navType = 'navigate';
  globalThis.caches = cachesMock;
  sessionStorageMock.setItem(SKIP_KEY, '1');
  fetchFactories.set(DATA, dataFactoryOK(1000));
  fetchFactories.set(WASM, wasmFactoryOK(2000));

  runScript();
  await settle();

  const matchesForKickUrls = cacheCalls.match.filter((c) => c.url === DATA || c.url === WASM).length;
  if (matchesForKickUrls !== 0) fail('skip-key kickoff must bypass cache.match, got ' + matchesForKickUrls);
  if (countCalls(DATA) !== 1 || countCalls(WASM) !== 1) fail('skip-key kickoff must still fetch data+wasm once each');
  if (sessionStorageMock.getItem(SKIP_KEY) !== null) fail('SKIP_KEY must be consumed(removed) after script init');
}

async function scenarioShortReadRetry() {
  deviceMemory = undefined;
  navType = 'navigate';
  globalThis.caches = cachesMock;
  fetchFactories.set(DATA, (callIndex) => callIndex === 0
    ? { status: 200, headers: { 'Content-Type': 'application/octet-stream', 'Content-Length': '1000' }, body: stdBody(500) }
    : { status: 200, headers: { 'Content-Type': 'application/octet-stream', 'Content-Length': '1000' }, body: stdBody(1000) });
  fetchFactories.set(WASM, wasmFactoryOK(2000));

  runScript();
  await settle();

  if (countCalls(DATA) !== 2) fail('short read must trigger exactly one retry (2 total calls), got ' + countCalls(DATA));
  if (!logs.warn.some((s) => s.indexOf('retry') >= 0)) fail('expected a retry warn log');

  const res = await window.fetch(DATA);
  if (!res || !res.ok) fail('joined response after retry not ok');
  if (countCalls(DATA) !== 2) fail('join must not add another network call, got ' + countCalls(DATA));
}

async function scenarioCeNoLengthCheck() {
  deviceMemory = undefined;
  navType = 'navigate';
  globalThis.caches = cachesMock;
  fetchFactories.set(DATA, () => ({ status: 200, headers: { 'Content-Type': 'application/octet-stream', 'Content-Encoding': 'br', 'Content-Length': '999' }, body: stdBody(700) }));
  fetchFactories.set(WASM, wasmFactoryOK(2000));

  runScript();
  await settle();

  if (countCalls(DATA) !== 1) fail('CE response must succeed without retry despite length mismatch, got ' + countCalls(DATA));
  if (logs.warn.some((s) => s.indexOf('retry') >= 0)) fail('CE response must not trigger retry (length check skipped)');

  const res = await window.fetch(DATA);
  if (!res || !res.ok) fail('CE joined response not ok');
}

async function main() {
  switch (scenario) {
    case 'kick_and_join': await scenarioKickAndJoin(); break;
    case 'bodyused_fallback': await scenarioBodyUsedFallback(); break;
    case 'low_memory_bare_fetch': await scenarioLowMemory(); break;
    case 'signal_bypass': await scenarioSignalBypass(); break;
    case 'reload_no_kick': await scenarioReloadNoKick(); break;
    case 'skip_key_bypass': await scenarioSkipKeyBypass(); break;
    case 'reload_low_memory_bare_fetch': await scenarioReloadLowMemory(); break;
    case 'reload_skip_key_bypass': await scenarioReloadSkipKey(); break;
    case 'short_read_retry': await scenarioShortReadRetry(); break;
    case 'ce_no_length_check': await scenarioCeNoLengthCheck(); break;
    default: fail('unknown scenario ' + scenario); return;
  }
  process.stdout.write('HARNESS_OK\n');
  process.exit(0);
}

main().catch((e) => fail('uncaught: ' + (e && e.stack ? e.stack : e)));
";

    private static string Legacy() =>
        WebGLBuildCopier.GenerateEarlyFetchScriptLegacyCaching(UrlsJson, CacheName, KickUrlsJson);

    // 생성 스크립트는 '<script>\n ... \n</script>' 로 래핑되어 있다 — Node에 넘길 순수 JS
    // 본문만 벗겨낸다.
    private static string ExtractScriptBody(string wrapped)
    {
        int tagEnd = wrapped.IndexOf('>');
        Assert.GreaterOrEqual(tagEnd, 0, "<script> 시작 태그를 찾을 수 없습니다.");
        int start = tagEnd + 1;
        int end = wrapped.LastIndexOf("</script>", StringComparison.Ordinal);
        Assert.Greater(end, start, "</script> 종료 태그를 찾을 수 없거나 시작 태그보다 앞에 있습니다.");
        return wrapped.Substring(start, end - start);
    }

    // 시나리오 하나를 임시 디렉토리에 스크립트/하네스를 써서 Node로 실행하고 계약 통과를 확인한다.
    private static void RunScenario(string scenarioName)
    {
        string nodePath = AITPackageManagerHelper.FindExecutable("node", verbose: false);
        if (string.IsNullOrEmpty(nodePath))
        {
            Assert.Ignore("Node 실행 파일 없음 — 런타임 실행 테스트 건너뜀");
        }

        string scriptBody = ExtractScriptBody(Legacy());

        string tempDir = Path.Combine(Path.GetTempPath(), "ait-early-fetch-runtime-" + Guid.NewGuid().ToString("N"));
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
    public void KickAndJoin_LoaderFetchJoinsPendingKickoff_NoDoubleDownload()
    {
        // 콜드 로드: caches 가용(MISS) + deviceMemory undefined.
        // 킥오프가 data/wasm을 각 1회만 네트워크 요청하고, 로더가 뒤이어 같은 URL을 fetch하면
        // 이중 다운로드 없이 pending에 합류(early-join)해야 한다.
        RunScenario("kick_and_join");
    }

    [Test]
    public void BodyUsedFallback_ConsumedPendingResponse_FallsBackToOriginalFetch()
    {
        // caches 자체가 없어(hasCache=false) 킥오프가 bare originalFetch를 사용하는 경로에서,
        // pending 응답의 body가 이미 소비(bodyUsed=true)된 경우 재사용하지 않고
        // originalFetch로 재요청해야 한다(로더가 동일 응답을 두 번 읽는 사고 방지).
        RunScenario("bodyused_fallback");
    }

    [Test]
    public void LowMemory_KickoffUsesBareFetch_NoBufferingNoCache()
    {
        // navigator.deviceMemory < 4 (저메모리 기기): 킥오프가 caches.open을 전혀 호출하지 않고
        // 버퍼링(arrayBuffer 소비) 없이 bare 스트리밍 fetch를 사용해야 한다(OOM 방어).
        RunScenario("low_memory_bare_fetch");
    }

    [Test]
    public void SignalBypass_LoaderAbortSignal_DelegatesToOriginalFetch()
    {
        // 로더가 init.signal을 넘기면 pending 재사용 없이 실제 인자로 originalFetch에 위임해
        // 취소 시맨틱을 보존해야 한다. 이후 pendingEarly가 소진되어 signal 없는 재요청은
        // 캐시 경로(match)로 진행해야 한다.
        RunScenario("signal_bypass");
    }

    [Test]
    public void Reload_SkipsKickoffEntirely_FallsBackToNormalCachePath()
    {
        // performance.getEntriesByType('navigation')[0].type === 'reload' 이면 킥오프 자체를
        // 생략해야 한다(이전 문서 keep-alive 소켓 경합 방지). 이후 fetch는 일반 캐시 경로로
        // 정상 동작해야 한다.
        RunScenario("reload_no_kick");
    }

    [Test]
    public void SkipKey_BypassesCacheMatchOnKickoff_AndIsConsumed()
    {
        // sessionStorage의 SKIP_KEY(__ait_skip_data_cache__)가 설정되어 있으면 킥오프가
        // caches.match를 건너뛰고 bufferedFetch로 직행해야 하며, SKIP_KEY는 1회성으로
        // 소비(제거)되어야 한다.
        RunScenario("skip_key_bypass");
    }

    [Test]
    public void LowMemory_ReloadFetchUsesBareOverridePath_NoCacheTouchNoBuffering()
    {
        // isReload=true + deviceMemory<4: 킥오프 루프 자체가 생략되어(isReload) pendingEarly가
        // 비고, window.fetch 오버라이드의 '!cacheOK -> bare originalFetch' 분기가 유일한 실행
        // 경로가 된다. 이 분기가 없으면 caches.open을 거쳐 저메모리 OOM 방어가 무력화된다.
        RunScenario("reload_low_memory_bare_fetch");
    }

    [Test]
    public void SkipKey_ReloadFetchBypassesCacheMatch_DirectToBufferedFetch()
    {
        // isReload=true + SKIP_KEY: 킥오프 루프가 생략되어(isReload) pendingEarly가 비고,
        // window.fetch 오버라이드의 skipCacheOnce 분기(cache.match 우회 후 bufferedFetch 직행)가
        // 유일한 실행 경로가 된다. 이 분기가 무력화되면 워치독 복구 reload에서 오염된 캐시를
        // 그대로 match해 서빙할 수 있다(self-amplification 방어 실효).
        RunScenario("reload_skip_key_bypass");
    }

    [Test]
    public void ShortRead_RetriesOnceThenSucceeds()
    {
        // Content-Length와 실제 수신 바이트 수가 불일치(스트림 조기 종료)하면 재시도해야 하며,
        // 재시도 성공 시 로더에는 완결된 응답을 반환해야 한다.
        RunScenario("short_read_retry");
    }

    [Test]
    public void ContentEncoding_SkipsLengthCheck_SucceedsWithoutRetry()
    {
        // Content-Encoding 응답(.br/.gz 네이티브 서빙)은 압축 크기(Content-Length)와 해제된
        // 본문 크기가 정의상 항상 달라, 길이 대조를 생략해야 한다(생략하지 않으면 성공 불가능한
        // 재다운로드 루프에 빠진다).
        RunScenario("ce_no_length_check");
    }
}
