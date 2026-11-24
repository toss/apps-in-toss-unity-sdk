// @ts-check
// C# 에디터 생성기(AITPageCacheEmitter / WebGLBuildCopier.EarlyFetch)가 내보내는 인라인 스크립트를 Unity 없이 얻는 도우미.
// 소스의 verbatim 문자열 상수(@"..." / $@"...")를 그대로 읽어 C# 과 같은 순서로 조립한다. 생성기 로직이 바뀌면 이 조립부도 같이 고친다
// (조립 결과는 mono 로 컴파일한 실제 생성기 출력과 바이트 단위로 비교해 확인했다: perf-lowmem-tier.test.js 의 주석 참고).
// 이 디렉터리(lib/) 파일은 이름이 *.test.js/*.spec.js가 되면 안 된다(playwright 기본 testMatch에 잡힘).
import * as fs from 'fs';
import * as path from 'path';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const PACKAGE_EDITOR_DIR = path.resolve(__dirname, '../../../../Editor/Package');
const RUNTIME_DIR = path.resolve(__dirname, '../../../../WebGLTemplates/AITTemplate/Runtime');

/** @param {string} file */
function readCs(file) {
  return fs.readFileSync(path.join(PACKAGE_EDITOR_DIR, file), 'utf8').replace(/\r\n/g, '\n');
}

/**
 * src[start] 가 verbatim 문자열의 여는 따옴표 '다음' 문자라고 보고, 닫는 따옴표까지 읽는다(`""` 는 따옴표 하나).
 * @param {string} src
 * @param {number} start
 */
function readVerbatim(src, start) {
  let out = '';
  for (let i = start; i < src.length; i++) {
    const c = src[i];
    if (c === '"') {
      if (src[i + 1] === '"') { out += '"'; i++; continue; }
      return out;
    }
    out += c;
  }
  throw new Error('verbatim 문자열이 닫히지 않음');
}

/**
 * `const string NAME = @"...";` 의 본문.
 * @param {string} src
 * @param {string} name
 */
function constString(src, name) {
  const m = new RegExp('const string ' + name + ' = @"').exec(src);
  if (!m) throw new Error('상수를 찾지 못함: ' + name);
  return readVerbatim(src, m.index + m[0].length);
}

/**
 * 메서드 안의 첫 `return $@"..."` 본문(보간 전). 중괄호 이스케이프(`{{`/`}}`)는 아직 풀지 않는다.
 * @param {string} src
 * @param {string} methodName
 */
function interpolatedReturn(src, methodName) {
  const sig = src.indexOf('string ' + methodName + '(');
  if (sig < 0) throw new Error('메서드를 찾지 못함: ' + methodName);
  const marker = 'return $@"';
  const at = src.indexOf(marker, sig);
  if (at < 0) throw new Error('return $@" 를 찾지 못함: ' + methodName);
  return readVerbatim(src, at + marker.length);
}

/**
 * 보간 문자열을 푼다: `{name}` 은 vars 로 치환하고 `{{`/`}}` 는 한 글자 중괄호로 되돌린다.
 * @param {string} raw
 * @param {Record<string, string>} vars
 */
function interpolate(raw, vars) {
  let s = raw.replace(/\{\{/g, '\u0001').replace(/\}\}/g, '\u0002');
  s = s.replace(/\{([A-Za-z_][A-Za-z0-9_.]*)\}/g, (_m, key) => {
    if (!(key in vars)) throw new Error('보간 변수 누락: ' + key);
    return vars[key];
  });
  return s.replace(/\u0001/g, '{').replace(/\u0002/g, '}');
}

/** @param {string} s <script> 래퍼를 벗긴 JS */
function stripScriptTags(s) {
  return s.replace(/^\s*<script>/, '').replace(/<\/script>\s*$/, '');
}

/** C# 생성기의 JsString/작은따옴표 리터럴과 같은 이스케이프. @param {string | null | undefined} v */
function singleQuoted(v) {
  return "'" + (v || '').replace(/\\/g, '\\\\').replace(/'/g, "\\'") + "'";
}

/** @param {string} v */
function doubleQuoted(v) {
  return '"' + v.replace(/\\/g, '\\\\').replace(/"/g, '\\"') + '"';
}

/**
 * AITPageCacheEmitter.GenerateInterceptorScript 와 같은 조립. 반환값은 <script> 래퍼를 벗긴 JS.
 * @param {{ cacheName?: string, dataFile?: string, frameworkFile?: string, wasmFile?: string, native?: boolean, deferFlag?: number, wrapped?: boolean }} [o]
 */
export function emitPageCacheScript(o = {}) {
  const src = readCs('AITPageCacheEmitter.cs');
  const dataFile = o.dataFile ?? 'aaaa.data';
  const frameworkFile = o.frameworkFile ?? 'cccc.framework.js';
  const wasmFile = o.wasmFile ?? 'bbbb.wasm';
  const cacheName = o.cacheName ?? 'ait-page-cache-test';
  const allow = ['Build/' + dataFile, 'Build/' + frameworkFile, 'Build/' + wasmFile];
  const peek = constString(src, 'PeekLowTierJs');
  const afterFirst = constString(src, 'AfterFirstFrameJs');
  const full = constString(src, 'ScriptOpenJs')
    + '\n            var CACHE_NAME = (window.__AIT_CACHE_NAME) || ' + doubleQuoted(cacheName) + ';'
    + '\n            var ALLOWLIST = [' + allow.map(doubleQuoted).join(',') + '];'
    + '\n            var WASM_LIST = [' + doubleQuoted('Build/' + wasmFile) + '];'
    + '\n            var NATIVE_SOURCE = ' + (o.native ? 'true' : 'false') + ';'
    + '\n            var DEFER_FLAG_BAKED = ' + String(o.deferFlag ?? -1) + ';'
    + peek + afterFirst
    + constString(src, 'BakedTailJs')
    + constString(src, 'IdbBackendJs')
    + constString(src, 'CacheCoreJs')
    + constString(src, 'FetchOverrideJs')
    + constString(src, 'SweepDumpCloseJs');
  return o.wrapped ? full : stripScriptTags(full);
}

/**
 * WebGLBuildCopier.GenerateEarlyFetchScriptModern. 반환값은 <script> 래퍼를 벗긴 JS.
 * @param {string[]} kickUrls
 * @param {string | null} [dataUrl]
 */
export function emitEarlyFetchModern(kickUrls, dataUrl = null) {
  const src = readCs('WebGLBuildCopier.EarlyFetch.cs');
  const peek = constString(readCs('AITPageCacheEmitter.cs'), 'PeekLowTierJs');
  const raw = interpolatedReturn(src, 'GenerateEarlyFetchScriptModern');
  return stripScriptTags(interpolate(raw, {
    urlsJson: JSON.stringify(kickUrls),
    dataUrlJs: singleQuoted(dataUrl),
    'AITPageCacheEmitter.PeekLowTierJs': peek,
  }));
}

/**
 * WebGLBuildCopier.GenerateEarlyFetchScriptLegacyCaching. 반환값은 <script> 래퍼를 벗긴 JS.
 * @param {{ urls: string[], kickUrls: string[], cacheName?: string, wasmUrl?: string | null, dataUrl?: string | null }} o
 */
export function emitEarlyFetchLegacy(o) {
  const src = readCs('WebGLBuildCopier.EarlyFetch.cs');
  const pc = readCs('AITPageCacheEmitter.cs');
  const boot = constString(pc, 'PeekLowTierJs') + constString(pc, 'AfterFirstFrameJs');
  const raw = interpolatedReturn(src, 'GenerateEarlyFetchScriptLegacyCaching');
  return stripScriptTags(interpolate(raw, {
    urlsJson: JSON.stringify(o.urls),
    kickUrlsJson: JSON.stringify(o.kickUrls),
    wasmUrlJs: singleQuoted(o.wasmUrl),
    dataUrlJs: singleQuoted(o.dataUrl),
    cacheName: o.cacheName ?? 'ait-unity-test-1-2-3',
    'AITPageCacheEmitter.BootSharedJs': boot,
    'AITPageCacheEmitter.PeekLowTierJs': constString(pc, 'PeekLowTierJs'),
  }));
}

/** @param {string} name Runtime 디렉터리의 템플릿 스크립트 이름 */
export function readRuntimeScript(name) {
  return fs.readFileSync(path.join(RUNTIME_DIR, name), 'utf8');
}
