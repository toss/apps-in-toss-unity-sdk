// -----------------------------------------------------------------------
// AITEarlyFetchZeroCopyTests.cs - 레거시(2021/2022) early-fetch 의 data/wasm 핸드오프 복사 제거 검증
//
//  배경: 레거시 경로는 data/wasm 을 arrayBuffer() 로 끝까지 받은 뒤(재시도/길이 대조/원자 put 을 위해)
//  new Response(buf) 로 로더에 넘기고 같은 buf 를 페이지 캐시에 new Response(buf) 로 put 했다.
//  엔진은 new Response(BufferSource) 마다 본문을 통째로 복사하므로 data+wasm 이 로더 읽기 구간에
//  버퍼 크기만큼 여러 번 더 상주했다(2021.3 CI 페어: exactDataBody 가 6000.x 는 42MB, 2021.3 은 10MB 만 감소).
//
//  변경: (1) 로더/index.html 에 주는 Response 는 이미 받은 Uint8Array 를 한 번 enqueue 하는 기본
//  ReadableStream 본문(복사 0), (2) 페이지 캐시 put 은 직렬화. 핸드오프는 window.__AIT_PERF.exactDataBody === false 로 끌 수 있고,
//  엔진이 JS ReadableStream 본문을 모르면(문자열 강제 변환) body 동일성 검사로 기존 방식에 되돌아간다.
//
//  put 을 버퍼의 subarray 뷰를 흘리는 스트림 본문으로 만들던 시도는 되돌렸다: 엔진이 청크마다 메인 스레드의 pull 을 기다려
//  CPU 4x 스로틀에서 put 이 5초 -> 30초 이상으로 늘고, 그동안 data 버퍼와 put 임시 복사가 상주해 2021.3 CI 페어에서
//  첫 프레임 뒤 RSS +30MB, 피크 +11MB 였다. put 은 new Response(buf) 한 덩어리로 돌아갔고(로더 버퍼와 공유하지 않는다) 이 테스트가 그것을 고정한다.
//
//  이 테스트는 실제 emitter/생성기 출력(페이지 캐시 스니펫 + 레거시 early-fetch 스크립트)을 Node 에서
//  head 에서와 같은 순서로 실행한다. 하네스 패턴은 AITEarlyFetchRuntimeTests 와 같다
//  (Node 미탐지 시 Assert.Ignore, ASSERT_FAIL/HARNESS_OK 프로토콜). 줄 단위 정규식은 쓰지 않는다
//  (Windows 체크아웃 CRLF 에서도 같은 결과여야 한다).
// -----------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using AppsInToss;
using AppsInToss.Editor;
using AppsInToss.Editor.Package;

[TestFixture]
public class AITEarlyFetchZeroCopyTests
{
    private const string CacheName = "ait-unity-test-1-2-3";
    private const string PageCacheName = "ait-page-cache-test";
    private const string DataFile = "aaaa.data.br";
    private const string WasmFile = "bbbb.wasm.br";
    private const string FrameworkFile = "cccc.framework.js.br";
    private const string LoaderFile = "dddd.loader.js";
    private const string UrlsJson =
        "[\"Build/aaaa.data.br\",\"Build/bbbb.wasm.br\",\"Build/cccc.framework.js.br\",\"Build/dddd.loader.js\"]";
    private const string KickUrlsJson =
        "[\"Build/aaaa.data.br\",\"Build/bbbb.wasm.br\"]";

    // Node 하네스: 페이지 캐시 스니펫과 early-fetch 스크립트를 (0, eval)로 실행하고 시나리오별로 계약을 assert 한다.
    // fetch/caches 는 mock(캐시 put 은 받은 Response 본문을 청크 단위로 읽어 기록), Response/ReadableStream 은 Node 네이티브.
    private const string HarnessSource = @"import { readFileSync } from 'node:fs';

const pageCacheBody = readFileSync(process.argv[2], 'utf8');
const earlyFetchBody = readFileSync(process.argv[3], 'utf8');
const scenario = process.argv[4];

const ORIGIN = 'https://game.example.com';
const DATA = ORIGIN + '/Build/' + 'aaaa.data.br';
const WASM = ORIGIN + '/Build/' + 'bbbb.wasm.br';
const DATA_SIZE = 9 * 1048576 + 123; // STREAM_CHUNK(4MB) 를 넘겨 청크가 3개가 되는 비정렬 크기
const WASM_SIZE = 5 * 1048576 + 7;

const logs = { log: [], warn: [], error: [] };
globalThis.console = {
  log: (...a) => { logs.log.push(a.join(' ')); },
  warn: (...a) => { logs.warn.push(a.join(' ')); },
  error: (...a) => { logs.error.push(a.join(' ')); },
};

function defineGlobal(name, value) {
  Object.defineProperty(globalThis, name, { value, writable: true, configurable: true, enumerable: true });
}
function fail(reason) {
  process.stderr.write('ASSERT_FAIL: ' + reason + '\n');
  process.stderr.write('LOGS=' + JSON.stringify(logs) + '\n');
  process.stderr.write('FETCH_CALLS=' + JSON.stringify(fetchCalls) + '\n');
  process.exit(1);
}
async function settle(times = 12) { for (let i = 0; i < times; i++) await new Promise((r) => setTimeout(r, 0)); }

function pattern(n) {
  const u = new Uint8Array(n);
  for (let i = 0; i < n; i++) u[i] = (i * 31 + (i >> 8)) & 255;
  return u;
}
function sameBytes(a, b) {
  if (a.length !== b.length) return false;
  for (let i = 0; i < a.length; i += 977) if (a[i] !== b[i]) return false;
  return a[a.length - 1] === b[b.length - 1];
}

const fetchCalls = [];
const sources = new Map();
async function mockFetch(resource, init) {
  const url = typeof resource === 'string' ? resource : resource.url;
  fetchCalls.push({ url, method: (init && init.method) || 'GET' });
  const src = sources.get(url);
  if (!src) throw new Error('mockFetch: no source for ' + url);
  // 압축 전송 모사: Content-Encoding 이 있으면 Content-Length 는 전송 크기(작은 값)다. 본문은 해제된 바이트.
  return new Response(src, { status: 200, headers: { 'Content-Type': url === WASM ? 'application/wasm' : 'application/octet-stream', 'Content-Encoding': 'br', 'Content-Length': '12345' } });
}

// 캐시 mock: put 된 Response 를 청크 단위로 읽어 기록한다(저장소가 하는 일과 같다).
const cacheStore = new Map();
const putInfo = {};
let putGate = null; // 시나리오가 put 완료 시점을 제어
const cacheCalls = { put: [], putDone: [] };
const cachesMock = {
  open: async () => ({
    match: async (url) => cacheStore.get(url),
    put: async (url, resp) => {
      cacheCalls.put.push(url);
      const info = { streamBody: false, chunkLens: [], buffers: new Set(), bytes: 0, headerLength: resp.headers.get('content-length') };
      const rd = resp.body.getReader();
      const parts = [];
      for (;;) {
        const { value, done } = await rd.read();
        if (done) break;
        info.chunkLens.push(value.length);
        info.buffers.add(value.buffer);
        info.bytes += value.length;
        parts.push(value);
      }
      info.first = parts[0];
      info.last = parts[parts.length - 1];
      putInfo[url] = info;
      if (putGate) await putGate(url);
      cacheStore.set(url, resp);
      cacheCalls.putDone.push(url);
    },
    keys: async () => Array.from(cacheStore.keys()).map((u) => ({ url: u })),
    delete: async (e) => cacheStore.delete(e && e.url ? e.url : e),
  }),
};

let navUA = 'Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1';
defineGlobal('window', globalThis);
defineGlobal('self', globalThis);
defineGlobal('location', { href: ORIGIN + '/', origin: ORIGIN });
defineGlobal('isSecureContext', true);
defineGlobal('indexedDB', undefined);
defineGlobal('caches', cachesMock);
defineGlobal('fetch', mockFetch);
defineGlobal('navigator', { userAgent: navUA, deviceMemory: 8 });
defineGlobal('performance', { getEntriesByType: () => [{ type: 'navigate' }] });
defineGlobal('sessionStorage', { getItem: () => null, setItem() {}, removeItem() {} });

function runBoth() {
  try { (0, eval)(pageCacheBody); } catch (e) { fail('page cache script threw: ' + (e && e.stack)); }
  try { (0, eval)(earlyFetchBody); } catch (e) { fail('early-fetch script threw: ' + (e && e.stack)); }
}

async function readAll(res) {
  const rd = res.body.getReader();
  const chunks = [];
  for (;;) {
    const { value, done } = await rd.read();
    if (done) break;
    chunks.push(value);
  }
  return chunks;
}

// ---- 시나리오 ----
async function scenarioZeroCopy() {
  window.__AIT_PERF = { exactDataBody: true };
  const src = pattern(DATA_SIZE);
  sources.set(DATA, src);
  sources.set(WASM, pattern(WASM_SIZE));
  runBoth();
  await settle(30);
  // 로더가 같은 URL 을 fetch → 킥오프 pending 에 합류.
  const res = await window.fetch(DATA);
  if (!res || !res.ok) fail('data joined response not ok');
  if (res.headers.get('content-length') !== String(DATA_SIZE)) fail('Content-Length must be exact RAW size, got ' + res.headers.get('content-length'));
  if (res.headers.get('content-encoding')) fail('Content-Encoding must be absent on handoff response');
  if (res.bodyUsed) fail('handoff response must be unread');
  const chunks = await readAll(res);
  if (chunks.length !== 1) fail('loader must receive one chunk (the buffer itself), got ' + chunks.length);
  if (chunks[0].length !== DATA_SIZE) fail('chunk length mismatch ' + chunks[0].length);
  if (!sameBytes(chunks[0], src)) fail('chunk bytes mismatch');
  await settle(30);
  const pi = putInfo[DATA];
  if (!pi) fail('data must be put into page cache');
  if (pi.bytes !== DATA_SIZE) fail('put total bytes mismatch ' + pi.bytes);
  // put 은 JS 스트림 본문(subarray 뷰 청크)이 아니라 new Response(buf) 한 덩어리다: 스트림 본문 put 은 스로틀 아래서 5초 -> 30초 이상으로 느려져 버퍼가 오래 상주한다.
  if (pi.chunkLens.length !== 1) fail('put body must be the single whole-buffer Response(buf), got chunk lens ' + JSON.stringify(pi.chunkLens));
  if (pi.buffers.has(chunks[0].buffer)) fail('put must not stream views of the loader buffer (stream-body put is slow and retains the buffer)');
  // 로더 청크는 put 과 무관하게 온전해야 한다(detach/transfer 금지).
  if (chunks[0].buffer.byteLength !== DATA_SIZE) fail('loader buffer must not be detached/transferred');
  if (pi.headerLength !== String(DATA_SIZE)) fail('put Content-Length header must be exact, got ' + pi.headerLength);
  // wasm 도 같은 스트림 경로.
  const wres = await window.fetch(WASM);
  const wchunks = await readAll(wres);
  if (wchunks.length !== 1 || wchunks[0].length !== WASM_SIZE) fail('wasm handoff chunk mismatch');
  if (wres.headers.get('content-type') !== 'application/wasm') fail('wasm Content-Type must be preserved');
  if (fetchCalls.filter((c) => c.url === DATA).length !== 1) fail('data must be downloaded once');
}

async function scenarioExactOff() {
  window.__AIT_PERF = { exactDataBody: false };
  const src = pattern(DATA_SIZE);
  sources.set(DATA, src);
  sources.set(WASM, pattern(WASM_SIZE));
  runBoth();
  await settle(30);
  const res = await window.fetch(DATA);
  if (res.headers.get('content-length') !== String(DATA_SIZE)) fail('Content-Length must be exact RAW size');
  const chunks = await readAll(res);
  let total = 0; for (const c of chunks) total += c.length;
  if (total !== DATA_SIZE) fail('total bytes mismatch ' + total);
  await settle(30);
  const pi = putInfo[DATA];
  if (!pi || pi.bytes !== DATA_SIZE) fail('put must still happen');
  // 기존 방식(new Response(buf)) 이면 put 본문은 한 덩어리 복사본이고 로더가 받은 청크와 버퍼를 공유하지 않는다.
  if (pi.chunkLens.length !== 1) fail('exactDataBody=false must keep the single-chunk Response(buf) put body, got ' + JSON.stringify(pi.chunkLens));
  for (const c of chunks) if (pi.buffers.has(c.buffer)) fail('exactDataBody=false must not share buffers between loader and put (old copy path)');
}

async function scenarioNoPerfObjectDefaultsOn() {
  delete globalThis.__AIT_PERF;
  sources.set(DATA, pattern(DATA_SIZE));
  sources.set(WASM, pattern(WASM_SIZE));
  runBoth();
  await settle(30);
  const res = await window.fetch(DATA);
  const chunks = await readAll(res);
  if (chunks.length !== 1) fail('no __AIT_PERF => default on (single chunk handoff), got ' + chunks.length);
}

async function scenarioLegacyEngineFallsBack() {
  // JS ReadableStream 본문을 모르는 구형 엔진: 생성자가 스트림을 문자열로 강제 변환한다.
  const NativeResponse = Response;
  class LegacyResponse extends NativeResponse {
    constructor(body, init) {
      if (body && typeof body === 'object' && typeof body.getReader === 'function') body = String(body);
      super(body, init);
    }
  }
  defineGlobal('Response', LegacyResponse);
  window.__AIT_PERF = { exactDataBody: true };
  const src = pattern(DATA_SIZE);
  sources.set(DATA, src);
  sources.set(WASM, pattern(WASM_SIZE));
  runBoth();
  await settle(30);
  const res = await window.fetch(DATA);
  const chunks = await readAll(res);
  let total = 0; for (const c of chunks) total += c.length;
  if (total !== DATA_SIZE) fail('fallback handoff must carry the real bytes, got ' + total);
  if (!sameBytes(chunks.length === 1 ? chunks[0] : Buffer.concat(chunks), src)) fail('fallback bytes mismatch (stringified stream leaked?)');
  await settle(30);
  const pi = putInfo[DATA];
  if (!pi || pi.bytes !== DATA_SIZE) fail('fallback put must store real bytes, got ' + (pi && pi.bytes));
}

async function scenarioPutSerialized() {
  window.__AIT_PERF = { exactDataBody: true };
  // 페이지 캐시만 실행(early-fetch 없이 훅을 직접 호출).
  try { (0, eval)(pageCacheBody); } catch (e) { fail('page cache script threw: ' + (e && e.stack)); }
  await settle();
  if (typeof window.__aitPageCachePutBuffer !== 'function') fail('put hook missing');
  let releaseFirst;
  putGate = (url) => (url === DATA ? new Promise((r) => { releaseFirst = r; }) : null);
  window.__aitPageCachePutBuffer(DATA, pattern(DATA_SIZE), 'application/octet-stream');
  window.__aitPageCachePutBuffer(WASM, pattern(WASM_SIZE), 'application/wasm');
  await settle(40);
  if (cacheCalls.put.length !== 1 || cacheCalls.put[0] !== DATA) fail('second put must wait for the first, put calls=' + JSON.stringify(cacheCalls.put));
  releaseFirst();
  await settle(40);
  if (cacheCalls.put.length !== 2 || cacheCalls.put[1] !== WASM) fail('second put must start after the first finished, put calls=' + JSON.stringify(cacheCalls.put));
  if (!window.__aitCacheStats.puts.includes(DATA) || !window.__aitCacheStats.puts.includes(WASM)) fail('stats.puts must record both');
}

async function scenarioPutFailureDoesNotBlockChain() {
  window.__AIT_PERF = { exactDataBody: true };
  try { (0, eval)(pageCacheBody); } catch (e) { fail('page cache script threw: ' + (e && e.stack)); }
  await settle();
  let n = 0;
  putGate = (url) => { if (url === DATA) throw new Error('quota'); return null; };
  window.__aitPageCachePutBuffer(DATA, pattern(DATA_SIZE), 'application/octet-stream');
  window.__aitPageCachePutBuffer(WASM, pattern(WASM_SIZE), 'application/wasm');
  await settle(60);
  if (!cacheCalls.put.includes(WASM)) fail('a failed put must not block the next one');
  if (!window.__aitCacheStats.errors.some((e) => e.indexOf('quota') >= 0)) fail('failed put must be recorded in stats.errors');
}

async function main() {
  switch (scenario) {
    case 'zero_copy': await scenarioZeroCopy(); break;
    case 'exact_off': await scenarioExactOff(); break;
    case 'no_perf_default_on': await scenarioNoPerfObjectDefaultsOn(); break;
    case 'legacy_engine_fallback': await scenarioLegacyEngineFallsBack(); break;
    case 'put_serialized': await scenarioPutSerialized(); break;
    case 'put_failure_no_block': await scenarioPutFailureDoesNotBlockChain(); break;
    default: fail('unknown scenario ' + scenario); return;
  }
  process.stdout.write('HARNESS_OK\n');
  process.exit(0);
}
main().catch((e) => fail('uncaught: ' + (e && e.stack ? e.stack : e)));
";

    private static string Legacy() =>
        WebGLBuildCopier.GenerateEarlyFetchScriptLegacyCaching(UrlsJson, CacheName, KickUrlsJson, "Build/" + WasmFile, "Build/" + DataFile);

    private static string PageCache()
    {
        var config = ScriptableObject.CreateInstance<AITEditorScriptObject>();
        try
        {
            config.pageCache = 1;
            config.pageCacheName = PageCacheName;
            config.nativeAssetSource = 0;
            return AITPageCacheEmitter.GenerateInterceptorScript(config, DataFile, FrameworkFile, WasmFile);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(config);
        }
    }

    // 생성 스크립트는 '<script> ... </script>' 로 래핑되어 있다 — Node 에 넘길 순수 JS 본문만 벗겨낸다.
    private static string ExtractScriptBody(string wrapped)
    {
        int tagEnd = wrapped.IndexOf('>');
        Assert.GreaterOrEqual(tagEnd, 0, "<script> 시작 태그를 찾을 수 없습니다.");
        int start = tagEnd + 1;
        int end = wrapped.LastIndexOf("</script>", StringComparison.Ordinal);
        Assert.Greater(end, start, "</script> 종료 태그를 찾을 수 없거나 시작 태그보다 앞에 있습니다.");
        return wrapped.Substring(start, end - start);
    }

    private static void RunScenario(string scenarioName)
    {
        string nodePath = AITPackageManagerHelper.FindExecutable("node", verbose: false);
        if (string.IsNullOrEmpty(nodePath))
        {
            Assert.Ignore("Node 실행 파일 없음 — 런타임 실행 테스트 건너뜀");
        }

        string pageCacheBody = ExtractScriptBody(PageCache());
        string earlyFetchBody = ExtractScriptBody(Legacy());

        string tempDir = Path.Combine(Path.GetTempPath(), "ait-early-fetch-zerocopy-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string pcPath = Path.Combine(tempDir, "pagecache.js");
        string efPath = Path.Combine(tempDir, "earlyfetch.js");
        string harnessPath = Path.Combine(tempDir, "harness.mjs");

        try
        {
            File.WriteAllText(pcPath, pageCacheBody);
            File.WriteAllText(efPath, earlyFetchBody);
            File.WriteAllText(harnessPath, HarnessSource);

            var startInfo = new ProcessStartInfo
            {
                FileName = nodePath,
                Arguments = $"\"{harnessPath}\" \"{pcPath}\" \"{efPath}\" {scenarioName}",
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

    // ---------------- 생성 스크립트 토큰 ----------------

    [Test]
    public void Legacy_EmbedsDataUrl_AndStreamHandoffHelpers()
    {
        string js = Legacy();

        StringAssert.Contains("var DATA_URL = 'Build/aaaa.data.br';", js);
        StringAssert.Contains("function loaderResponse(url, buf, ct)", js);
        StringAssert.Contains("return markNonNet(loaderResponse(url, buf, ct));", js);
        StringAssert.Contains("return c.put(url, new Response(buf, { status: 200, headers: h }));", js);
        // put 용 스트림 본문(subarray 뷰 청크)은 쓰지 않는다: 스로틀 아래서 put 이 5초 -> 30초 이상으로 늘어 버퍼가 상주한다.
        StringAssert.DoesNotContain("function putResponse", js);
        StringAssert.DoesNotContain("STREAM_CHUNK", js);
        StringAssert.DoesNotContain("buf.subarray", js);
        // 엔진이 JS ReadableStream 본문을 모르면 문자열로 강제 변환되므로 body 동일성 검사로 기존 방식에 되돌아간다.
        StringAssert.Contains("r.body === rs", js);
        // 로더/put 이 같은 버퍼를 공유하므로 transfer(detach)가 일어나는 byte 스트림은 쓰지 않는다.
        StringAssert.DoesNotContain("type: 'bytes'", js);
        StringAssert.DoesNotContain("type:'bytes'", js);
        // exactDataBody 끄기 스위치.
        StringAssert.Contains("pf.exactDataBody === false", js);
        // 기존 호출 계약은 유지.
        StringAssert.Contains("window.__aitPageCachePutBuffer(url, buf, ct);", js);
        StringAssert.Contains("storeBuffer(url, buf, ct);", js);
        // 재합성 Response 직접 생성(복사)이 로더 핸드오프 자리에 남아 있으면 안 된다.
        StringAssert.DoesNotContain("markNonNet(new Response(buf", js);
    }

    [Test]
    public void Legacy_WithoutDataUrl_DisablesDataStreamHandoff()
    {
        string js = WebGLBuildCopier.GenerateEarlyFetchScriptLegacyCaching(UrlsJson, CacheName, KickUrlsJson);

        StringAssert.Contains("var DATA_URL = '';", js);
        StringAssert.Contains("var DATA_ABS = '';", js);
    }

    [Test]
    public void Dispatcher_PassesDataUrl_ToLegacyScript()
    {
        string unityVersion = Application.unityVersion ?? "";
        int dot = unityVersion.IndexOf('.');
        int major;
        if (dot <= 0 || !int.TryParse(unityVersion.Substring(0, dot), out major) || major >= 6000)
        {
            Assert.Ignore("레거시(2021/2022) 에디터에서만 레거시 스크립트가 생성된다: " + unityVersion);
        }

        MethodInfo m = typeof(WebGLBuildCopier).GetMethod("GenerateEarlyFetchScript", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(m, "GenerateEarlyFetchScript 를 찾을 수 없습니다.");
        string js = (string)m.Invoke(null, new object[] { DataFile, FrameworkFile, WasmFile, LoaderFile, "1.0", 111L, 222L });

        StringAssert.Contains("var DATA_URL = 'Build/aaaa.data.br';", js);
        StringAssert.Contains("var WASM_URL = 'Build/bbbb.wasm.br';", js);
    }

    [Test]
    public void PageCache_PutBuffer_UsesWholeBufferResponse_AndSerialization()
    {
        string js = PageCache();

        StringAssert.Contains("c.put(url, new Response(buf, { status: 200, headers: h }))", js);
        // put 용 스트림 본문은 쓰지 않는다(느린 put 이 버퍼를 오래 붙든다).
        StringAssert.DoesNotContain("bufferPutResponse", js);
        StringAssert.DoesNotContain("PUT_CHUNK", js);
        StringAssert.DoesNotContain("u8.subarray", js);
        StringAssert.Contains("var putChain = Promise.resolve();", js);
        // put 감시 타이머와 통계 계약은 유지.
        StringAssert.Contains("'put timeout ' + url", js);
        StringAssert.Contains("window.__aitPageCachePutBuffer = function (url, buf, ct)", js);
    }

    // ---------------- 런타임 ----------------

    [Test]
    public void Runtime_LoaderGetsBufferItself_PutUsesWholeBufferResponse()
    {
        // 로더가 받는 data 청크가 버퍼 그 자체이고(복사 0), put 은 스트림 뷰가 아니라 new Response(buf) 한 덩어리다. wasm 도 같은 스트림 핸드오프.
        RunScenario("zero_copy");
    }

    [Test]
    public void Runtime_ExactDataBodyOff_KeepsLegacyResponseBufPath()
    {
        RunScenario("exact_off");
    }

    [Test]
    public void Runtime_MissingPerfObject_DefaultsOn()
    {
        RunScenario("no_perf_default_on");
    }

    [Test]
    public void Runtime_EngineWithoutStreamBodySupport_FallsBackToResponseBuf()
    {
        // new Response(readableStream) 이 스트림을 문자열로 강제 변환하는 구형 엔진: 오염된 응답이 로더/캐시에 가면 안 된다.
        RunScenario("legacy_engine_fallback");
    }

    [Test]
    public void Runtime_PagePut_IsSerialized()
    {
        RunScenario("put_serialized");
    }

    [Test]
    public void Runtime_FailedPut_DoesNotBlockNextPut()
    {
        RunScenario("put_failure_no_block");
    }
}
