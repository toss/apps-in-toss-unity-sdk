// @ts-check
// 프리웜 테스트용 픽스처: 빌드 산출물(dist/web)과 '현재 소스의' 템플릿(index.html + Runtime/*.js)을 합쳐 서빙한다.
//
// 빌드 산출물의 index.html 은 빌드 당시 템플릿이라 프리웜 로직이 없다. 그래서 산출물에서 '치환 값'만 읽어(Build/* 파일명, perf 플래그,
// 페이지 캐시·early-fetch 인라인 블록, 로딩 화면) 현재 템플릿의 플레이스홀더를 직접 치환한다(WebGLBuildCopier 의 치환 흉내).
// Runtime/*.js 는 템플릿 디렉터리 것을 쓴다. Unity 도 vite 도 필요 없다.
// 이 디렉터리(lib/) 파일은 이름이 *.test.js/*.spec.js가 되면 안 된다(playwright 기본 testMatch에 잡힘).
import * as fs from 'fs';
import * as http from 'http';
import * as path from 'path';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
export const TEMPLATE_DIR = path.resolve(__dirname, '../../../../WebGLTemplates/AITTemplate');

/** @param {string} dir */
export function hasBuild(dir) {
  try {
    return !!dir && fs.existsSync(path.join(dir, 'index.html')) && fs.existsSync(path.join(dir, 'Build'));
  } catch { return false; }
}

/**
 * 산출물 index.html 에서 `<!-- marker ... -->` 다음에 오는 첫 `<script>...</script>` 블록을 그대로 잘라 온다.
 * @param {string} built
 * @param {string} marker
 */
function blockAfter(built, marker) {
  const m = built.indexOf(marker);
  if (m < 0) return '';
  const s = built.indexOf('<script>', m);
  const e = built.indexOf('</script>', s);
  if (s < 0 || e < 0) return '';
  return built.slice(s, e + '</script>'.length);
}

/**
 * @param {string} distWeb dist/web 경로
 * @param {{ prewarm: boolean }} opts
 * @returns {string} 치환이 끝난 index.html
 */
export function buildFixtureHtml(distWeb, opts) {
  const built = fs.readFileSync(path.join(distWeb, 'index.html'), 'utf8');
  let html = fs.readFileSync(path.join(TEMPLATE_DIR, 'index.html'), 'utf8');

  const grab = (re, what) => {
    const m = re.exec(built);
    if (!m) throw new Error(`산출물 index.html 에서 ${what} 를 찾지 못했다`);
    return m[1];
  };
  const loaderUrl = grab(/<script src="(Build\/[^"]+\.loader\.js)"/, 'loader URL');
  const dataUrl = grab(/dataUrl: "([^"]+)"/, 'dataUrl');
  const frameworkUrl = grab(/frameworkUrl: "([^"]+)"/, 'frameworkUrl');
  const codeUrl = grab(/codeUrl: "([^"]+)"/, 'codeUrl');
  const perfFlags = grab(/JSON\.parse\('([^']*)'\)/, 'perf 플래그');
  const dpr = grab(/var aitDevicePixelRatioSetting = ([^;]+);/, 'DPR 설정');
  const ppEnabled = grab(/enabled: '([^']*)' !== 'false'/, 'PlayerPrefs 플래그');
  const loadingStart = built.indexOf('<!-- loading.html 내용이 여기에 삽입됨 -->');
  const loadingEnd = built.indexOf('</div>\n\n    <div id="unity-container"', loadingStart);
  const loading = loadingStart < 0 || loadingEnd < 0 ? '' : built.slice(loadingStart + '<!-- loading.html 내용이 여기에 삽입됨 -->'.length, loadingEnd);
  const pageCache = blockAfter(built, '<!-- AIT Page Cache');
  const earlyFetch = blockAfter(built, '<!-- Unity Resource Early Fetch');
  if (!pageCache || !earlyFetch) throw new Error('산출물 index.html 에서 페이지 캐시/early-fetch 블록을 찾지 못했다');

  // #if / #endif 지시문: 산출물에는 memory/symbols 파일이 없다 → 블록 통째로 제거.
  html = html.replace(/^#if [A-Z_]+\n[\s\S]*?^#endif\n/gm, '');

  // 토큰 하나라도 함수 치환기로 넣어 `$&` 같은 특수 패턴이 값에 있어도 안전하게 한다.
  const rep = (token, value) => { html = html.split(token).join(value); };
  rep('%AIT_PAGE_CACHE_SCRIPT%', pageCache);
  rep('%AIT_EARLY_FETCH_SCRIPT%', earlyFetch);
  rep('%AIT_LOADING_SCREEN%', loading);
  rep('%AIT_PERF_FLAGS%', perfFlags);
  rep('%AIT_PREWARM%', opts.prewarm ? 'true' : 'false');
  rep('%AIT_FIRST_INTERACTIVE_LOG%', 'true');
  rep('%AIT_PLAYERPREFS_PERSISTENCE%', ppEnabled);
  rep('%AIT_KEYBOARD_MODE%', 'pan');
  rep('%AIT_BUILD_VARIANT%', '');
  rep('%AIT_ICON_URL%', '');
  rep('%AIT_DISPLAY_NAME%', '');
  rep('%AIT_PRIMARY_COLOR%', '#3182f6');
  rep('%AIT_DEVICE_PIXEL_RATIO%', dpr);
  rep('%UNITY_WEBGL_LOADER_URL%', loaderUrl);
  rep('%UNITY_WEBGL_DATA_URL%', dataUrl);
  rep('%UNITY_WEBGL_FRAMEWORK_URL%', frameworkUrl);
  rep('%UNITY_WEBGL_CODE_URL%', codeUrl);
  rep('%UNITY_COMPANY_NAME%', 'Apps in Toss');
  rep('%UNITY_PRODUCT_NAME%', 'PrewarmFixture');
  rep('%UNITY_PRODUCT_VERSION%', '1.0');
  rep('%UNITY_WEB_NAME%', 'PrewarmFixture');
  rep('%UNITY_WIDTH%', '960');
  rep('%UNITY_HEIGHT%', '600');
  // 브리지 모듈: vite 가 번들한 산출물(assets/index.js)로 바꾼다.
  const bridge = /<script type="module"[^>]*src="\.\/assets\/index\.js"[^>]*><\/script>/.exec(built);
  html = html.replace(/<script type="module" src="\.\/unity-bridge\.ts"><\/script>/, bridge ? bridge[0] : '');

  const left = html.match(/%(?:AIT|UNITY)_[A-Z_]+%/g);
  if (left) throw new Error('치환되지 않은 플레이스홀더: ' + [...new Set(left)].join(', '));
  return html;
}

const MIME = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'application/javascript',
  '.json': 'application/json',
  '.css': 'text/css',
  '.png': 'image/png',
  '.ico': 'image/x-icon',
  '.wasm': 'application/wasm',
  '.data': 'application/octet-stream',
};

/**
 * 픽스처 서버. Build/*.gz·*.br 은 Content-Encoding: gzip·br 로(실서비스의 네이티브 서빙과 같다), 확장자는 압축 접미사를 벗긴 쪽의 MIME 으로 준다.
 * 비압축 빌드는 그대로 준다.
 * @param {string} distWeb
 * @param {{ prewarm: boolean, port?: number, throttleKBps?: number }} opts throttleKBps>0 이면 Build/* 응답을 그 속도로 흘려 보낸다(저속 망 흉내)
 * @returns {Promise<{ url: string, close: () => Promise<void>, requests: string[] }>}
 */
export async function startFixtureServer(distWeb, opts) {
  const html = buildFixtureHtml(distWeb, { prewarm: opts.prewarm });
  /** @type {string[]} */
  const requests = [];
  const server = http.createServer((req, res) => {
    const u = new URL(req.url || '/', 'http://x');
    let p = decodeURIComponent(u.pathname);
    requests.push(p);
    if (p === '/' || p === '/index.html') {
      res.writeHead(200, { 'Content-Type': MIME['.html'], 'Cache-Control': 'no-store' });
      res.end(html);
      return;
    }
    // Runtime/*.js 는 현재 소스의 템플릿 것을 쓴다.
    const rel = p.replace(/^\/+/, '');
    let file = rel.startsWith('Runtime/') ? path.join(TEMPLATE_DIR, rel) : path.join(distWeb, rel);
    if (!file.startsWith(rel.startsWith('Runtime/') ? TEMPLATE_DIR : distWeb) || !fs.existsSync(file) || fs.statSync(file).isDirectory()) {
      res.writeHead(404);
      res.end('not found');
      return;
    }
    const enc = file.endsWith('.gz') ? 'gzip' : file.endsWith('.br') ? 'br' : null;
    const ext = path.extname(enc ? file.slice(0, -3) : file);
    /** @type {Record<string, string>} */
    const headers = { 'Content-Type': MIME[ext] || 'application/octet-stream', 'Cache-Control': 'no-cache' };
    if (enc) headers['Content-Encoding'] = enc;
    const buf = fs.readFileSync(file);
    headers['Content-Length'] = String(buf.length);
    if (opts.throttleKBps && opts.throttleKBps > 0 && rel.startsWith('Build/')) {
      res.writeHead(200, headers);
      const chunk = Math.max(1024, Math.floor(opts.throttleKBps * 1024 / 20)); // 50ms 간격
      let off = 0;
      const tick = () => {
        if (off >= buf.length) { res.end(); return; }
        res.write(buf.subarray(off, off + chunk));
        off += chunk;
        setTimeout(tick, 50);
      };
      tick();
      return;
    }
    res.writeHead(200, headers);
    res.end(buf);
  });
  await new Promise((resolve) => server.listen(opts.port || 0, '127.0.0.1', () => resolve(undefined)));
  const addr = /** @type {import('net').AddressInfo} */ (server.address());
  return {
    url: `http://127.0.0.1:${addr.port}`,
    requests,
    close: () => new Promise((resolve) => { server.closeAllConnections?.(); server.close(() => resolve()); }),
  };
}
