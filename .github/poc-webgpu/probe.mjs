// WebGPU PoC probe: serve one build and record which graphics API Unity picked.
import { chromium } from '@playwright/test';
import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';

const [, , label, webDir, outDir] = process.argv;
const MIME = { '.html': 'text/html', '.js': 'text/javascript', '.mjs': 'text/javascript', '.wasm': 'application/wasm',
  '.json': 'application/json', '.css': 'text/css', '.png': 'image/png', '.data': 'application/octet-stream' };

const server = http.createServer((req, res) => {
  let p = decodeURIComponent(new URL(req.url, 'http://x').pathname);
  if (p.endsWith('/')) p += 'index.html';
  const f = path.join(webDir, p);
  if (!f.startsWith(webDir) || !fs.existsSync(f) || fs.statSync(f).isDirectory()) { res.writeHead(404); return res.end(); }
  res.writeHead(200, { 'Content-Type': MIME[path.extname(f)] || 'application/octet-stream' });
  fs.createReadStream(f).pipe(res);
});
await new Promise(r => server.listen(0, '127.0.0.1', r));
const port = server.address().port;

const INIT = () => {
  window.__poc = { ctx: [] };
  const orig = HTMLCanvasElement.prototype.getContext;
  HTMLCanvasElement.prototype.getContext = function (type, ...rest) {
    const r = orig.call(this, type, ...rest);
    window.__poc.ctx.push({ type, ok: !!r, id: this.id || this.tagName });
    return r;
  };
};

const modes = [
  { name: 'chromium-headless', opts: { headless: true, args: ['--use-angle=default'] } },
  { name: 'chromium-headless-unsafe', opts: { headless: true, args: ['--enable-unsafe-webgpu', '--use-angle=metal'] } },
  { name: 'chromium-headed', opts: { headless: false, args: [] } },
  { name: 'chrome-headed', opts: { headless: false, channel: 'chrome', args: [] } },
];

for (const m of modes) {
  const rec = { build: label, mode: m.name };
  let browser;
  try {
    browser = await chromium.launch(m.opts);
    rec.browser = browser.version();
    const page = await browser.newPage({ viewport: { width: 390, height: 844 } });
    const logs = [];
    page.on('console', msg => { const t = msg.text(); if (/webgpu|webgl|graphics|gpu|error|fallback/i.test(t)) logs.push(`${msg.type()}: ${t.slice(0, 300)}`); });
    page.on('pageerror', e => logs.push(`pageerror: ${String(e).slice(0, 300)}`));
    await page.addInitScript(INIT);
    await page.goto(`http://127.0.0.1:${port}/?e2e=true`, { waitUntil: 'domcontentloaded', timeout: 60000 });
    rec.gpu = await page.evaluate(async () => {
      if (!navigator.gpu) return { navigatorGpu: false };
      try {
        const a = await navigator.gpu.requestAdapter();
        if (!a) return { navigatorGpu: true, adapter: null };
        const i = a.info || {};
        return { navigatorGpu: true, adapter: { vendor: i.vendor, architecture: i.architecture, description: i.description }, fallback: !!a.isFallbackAdapter };
      } catch (e) { return { navigatorGpu: true, error: String(e) }; }
    });
    const t0 = Date.now();
    try {
      await page.waitForFunction(() => window.unityInstance !== undefined, null, { timeout: 120000 });
      rec.unityReadyMs = Date.now() - t0;
    } catch { rec.unityReadyMs = null; }
    await page.waitForTimeout(5000);
    rec.contexts = await page.evaluate(() => window.__poc.ctx);
    rec.canvas = await page.evaluate(() => {
      const c = document.querySelector('#unity-canvas') || document.querySelector('canvas');
      if (!c) return null;
      return { id: c.id, w: c.width, h: c.height };
    });
    rec.fps = await page.evaluate(() => new Promise(res => {
      let n = 0; const s = performance.now();
      const f = () => { n++; if (performance.now() - s < 3000) requestAnimationFrame(f); else res(Math.round(n / ((performance.now() - s) / 1000))); };
      requestAnimationFrame(f);
    }));
    await page.screenshot({ path: path.join(outDir, `${label}-${m.name}.png`) });
    rec.logs = logs.slice(0, 25);
  } catch (e) {
    rec.error = String(e).slice(0, 400);
  } finally {
    if (browser) await browser.close().catch(() => {});
  }
  fs.appendFileSync(path.join(outDir, 'results.jsonl'), JSON.stringify(rec) + '\n');
  console.log(JSON.stringify(rec));
}
server.close();
