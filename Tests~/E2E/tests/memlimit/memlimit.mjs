// 메모리 제한 환경에서 heavy 빌드가 부팅·생존하는 최소 cgroup 한도(min viable MB)를 찾는다.
//
// 사용:
//   node memlimit.mjs --dist <dist/web 경로> [--start 704] [--step 32] [--max-levels 12]
//        [--out result.json] [--image mcr.microsoft.com/playwright:v1.63.0-noble]
//   node memlimit.mjs --dist <dist/web> --limit 768          # 단일 한도 1회(스모크)
//
// 동작(C 모드 = 브라우저 컨테이너 전체에 cgroup 한도):
//  - 한도마다 새 컨테이너(`docker run --memory=N --memory-swap=N`)에 헤드리스 Chromium 을 띄우고
//    CDP 로 붙는다. 정적 서버는 호스트에서 돌리고, 컨테이너 안 Chromium 이 `localhost` 를
//    호스트로 매핑해 접속한다(origin 은 localhost 유지 → secure context).
//  - 한 번의 부팅 = iPhone 11 급 프로파일(CPU 4x 스로틀, 100/50Mbps, RTT 50ms) 로 로드 →
//    첫 프레임(TTFF) 감지 → 캔버스 클릭(오디오 제스처) → 30초 유지 → 생존/그리기 지속 확인.
//    스트리밍 오디오·폰트 에셋 로드는 [AIT-UnityMem] 로그로 확인한다.
//  - 한 한도의 통과 = 부팅 2회 모두 통과. 시작 한도에서 step 씩 내려가며 처음 실패하는 한도까지
//    (최대 max-levels 단계) 탐색한다. min viable = 마지막 통과 한도.
//  - 2-boot tier: 처음 실패한 한도와 그 한 단계 아래에서 1회 부팅이 죽으면 같은 컨텍스트에서 재부팅해
//    저메모리 tier(1) 로 살아나는지 본다(AITMemory.lowMemTier).
//  - R 모드(렌더러만 cgroup)는 컨테이너 안에서 cgroup 하위 트리를 직접 만들어야 해서(--privileged +
//    --renderer-cmd-prefix) CI 러너에서는 구현하지 않는다.
//
// 종료 코드: 0 = 측정 완료(수치가 나빠도 0), 2 = 인프라 오류(docker/CDP/서버 문제로 측정 불가).
import fs from 'fs';
import os from 'os';
import http from 'http';
import path from 'path';
import { fileURLToPath } from 'url';
import { spawnSync } from 'child_process';
import { chromium } from '@playwright/test';

const HERE = path.dirname(fileURLToPath(import.meta.url));
const argv = process.argv.slice(2);
const opt = (k, d) => { const i = argv.indexOf('--' + k); return i >= 0 ? argv[i + 1] : d; };

const DIST = path.resolve(opt('dist', ''));
const SINGLE = opt('limit', null);
const START = +opt('start', 704);
const STEP = +opt('step', 32);
const MAX_LEVELS = +opt('max-levels', 12);
const OUT = opt('out', null);
const IMAGE = opt('image', 'mcr.microsoft.com/playwright:v1.63.0-noble');
const HOLD = +opt('hold', 30);
const TTFF_TIMEOUT = +opt('ttff-timeout', 120);
const REQUIRE_STREAM = opt('require-stream', '1') === '1';
const CPUS = opt('cpus', String(Math.max(1, Math.min(3, os.cpus().length - 1))));
const HOST_ALIAS = 'ait-host'; // --add-host 로 host-gateway 에 매핑하는 컨테이너 내부 별칭


// iPhone 11 급: deviceMemory 미노출(WebKit 과 동일), 6코어.
const PROFILE = {
  ctx: {
    viewport: { width: 414, height: 896 }, screen: { width: 414, height: 896 }, deviceScaleFactor: 2, isMobile: true, hasTouch: true,
    userAgent: 'Mozilla/5.0 (iPhone; CPU iPhone OS 17_5 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.5 Mobile/15E148 Safari/604.1',
  },
  cores: 6,
};

if (!DIST || !fs.existsSync(path.join(DIST, 'index.html'))) { console.error(`--dist 에 index.html 이 없습니다: ${DIST}`); process.exit(2); }

const perfSrc = fs.readFileSync(path.join(HERE, '..', 'perf-ttff.test.js'), 'utf8');
const TTFF_INIT = (perfSrc.match(/const TTFF_INIT_SCRIPT = `([\s\S]*?)`;/) || [])[1];
if (!TTFF_INIT) { console.error('perf-ttff.test.js 에서 TTFF_INIT_SCRIPT 를 찾지 못했습니다'); process.exit(2); }
const LM_INIT = fs.readFileSync(path.join(HERE, 'lm-init.js'), 'utf8');

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const tmo = (p, ms, v) => Promise.race([p, new Promise((r) => setTimeout(() => r(v), ms))]);
const docker = (args) => spawnSync('docker', args, { encoding: 'utf8' });

// ---- 정적 서버(호스트). Brotli/Gzip 사전압축 파일은 Content-Encoding 으로 서빙 ----
const MIME = { '.html': 'text/html', '.js': 'application/javascript', '.json': 'application/json', '.css': 'text/css', '.png': 'image/png', '.ico': 'image/x-icon', '.svg': 'image/svg+xml', '.m4a': 'audio/mp4', '.wav': 'audio/wav' };
function startServer() {
  const root = DIST;
  const srv = http.createServer((req, res) => {
    let u = decodeURIComponent(req.url.split('?')[0]); if (u.endsWith('/')) u += 'index.html';
    const f = path.join(root, u);
    if (!f.startsWith(root) || !fs.existsSync(f) || fs.statSync(f).isDirectory()) { res.statusCode = 404; return res.end('nf'); }
    if (u.includes('/Build/')) res.setHeader('Cache-Control', 'no-store');
    let ct = MIME[path.extname(f)] || 'application/octet-stream';
    if (u.endsWith('.br') || u.endsWith('.gz')) {
      res.setHeader('Content-Encoding', u.endsWith('.br') ? 'br' : 'gzip');
      if (u.includes('.wasm.')) ct = 'application/wasm'; else if (u.includes('.js.')) ct = 'application/javascript';
    }
    res.setHeader('Content-Type', ct); res.setHeader('Content-Length', fs.statSync(f).size);
    if (req.method === 'HEAD') return res.end();
    fs.createReadStream(f).pipe(res);
  });
  return new Promise((resolve) => srv.listen(0, '0.0.0.0', () => resolve({ srv, port: srv.address().port })));
}

// ---- 부팅 1회 ----
async function bootOnce(ctx, ctr, url, bi, state) {
  const b = { bi };
  let crashed = false, ttffSeen = false, lastSnap = null;
  const aitLines = [], consoleAll = [];
  const page = await ctx.newPage();
  const cdp = await ctx.newCDPSession(page);
  page.on('crash', () => { crashed = true; });
  page.on('dialog', (d) => { d.dismiss().catch(() => {}); });
  page.on('console', (m) => {
    const t = m.text();
    if (t.startsWith('[LM] ')) {
      const mm = t.match(/^\[LM\] (\S+) (\d+) ?(.*)$/);
      if (mm && mm[1] === 'snap') { try { lastSnap = JSON.parse(mm[3]); if (lastSnap.ttff != null) ttffSeen = true; } catch { /* 무시 */ } }
    } else {
      if (/\[AIT-|HeavyAudioProbe|Decode error|decodeAudio/i.test(t) && aitLines.length < 400) aitLines.push(t.slice(0, 300));
      if (m.type() === 'error' || /abort|Aborted|out of memory|OOM/i.test(t)) consoleAll.push(t.slice(0, 300));
    }
  });
  page.on('pageerror', (e) => consoleAll.push(String(e.message || e).slice(0, 300)));
  await cdp.send('Emulation.setCPUThrottlingRate', { rate: 4 });
  await cdp.send('Network.enable');
  await cdp.send('Network.emulateNetworkConditions', { offline: false, downloadThroughput: 100 * 1048576 / 8, uploadThroughput: 50 * 1048576 / 8, latency: 50 });
  page.goto(url, { waitUntil: 'commit', timeout: 30000 }).catch(() => {});

  const containerDead = () => docker(['inspect', '-f', '{{.State.Running}}', ctr]).stdout.trim() === 'false';
  const deadline = Date.now() + TTFF_TIMEOUT * 1000;
  while (Date.now() < deadline && !ttffSeen && !crashed && !state.disconnected) {
    await sleep(250);
    if (containerDead()) state.disconnected = true;
  }
  b.ttff = ttffSeen && lastSnap ? lastSnap.ttff : null;
  // 오디오 제스처: 첫 프레임 +1초에 캔버스 중앙 클릭, +5초에 한 번 더
  if (ttffSeen && !crashed && !state.disconnected) {
    await sleep(1000);
    for (const w of [0, 4000]) {
      if (w) await sleep(w);
      if (crashed || state.disconnected) break;
      await tmo(page.mouse.click(PROFILE.ctx.viewport.width / 2, PROFILE.ctx.viewport.height / 2).catch(() => {}), 5000);
    }
    const t1 = Date.now() + HOLD * 1000;
    while (Date.now() < t1 && !crashed && !state.disconnected) { await sleep(500); if (containerDead()) state.disconnected = true; }
  }
  b.crashed = crashed; b.disconnected = state.disconnected;
  if (!crashed && !state.disconnected) {
    const fin = await tmo(page.evaluate(() => ({
      draws: window.__lm.draws, raf: window.__lm.raf, heapMB: window.__lmHeapMB(),
      tier: window.AITMemory && window.AITMemory.lowMemTier, bootFail: window.AITMemory && window.AITMemory.bootFailCount,
    })).catch((e) => ({ evalErr: String(e.message).slice(0, 100) })), 15000, { evalErr: 'timeout15s' });
    b.fin = fin;
    if (fin && fin.draws != null) {
      await sleep(5000);
      const f2 = await tmo(page.evaluate(() => ({ draws: window.__lm.draws, raf: window.__lm.raf })).catch(() => null), 10000, null);
      b.live = f2 ? { drawsDelta: f2.draws - fin.draws, rafDelta: f2.raf - fin.raf } : { err: 'no-eval' };
    }
  }
  const um = aitLines.filter((l) => /AIT-UnityMem/.test(l)); const lastUm = um.length ? um[um.length - 1] : '';
  const cnt = (k) => { const m = lastUm.match(new RegExp(k + '=([0-9.]+)MB\\((\\d+)\\)')); return m ? { mb: +m[1], n: +m[2] } : null; };
  b.streamAudio = cnt('streamAudio'); b.streamFont = cnt('streamFont');
  b.streamOk = !!(b.streamAudio && b.streamAudio.n > 0 && b.streamFont && b.streamFont.n > 0);
  b.aborted = consoleAll.some((c) => /abort\(|Aborted\(|out of memory|Cannot enlarge memory|OOM/i.test(c));
  const alive = !crashed && !state.disconnected && b.fin && !b.fin.evalErr;
  const drawing = b.live && b.live.drawsDelta > 0 && b.live.rafDelta > 0;
  b.alive = !!(ttffSeen && alive && drawing && !b.aborted);
  b.ok = b.alive && (!REQUIRE_STREAM || b.streamOk);
  if (!state.disconnected) await tmo(page.close().catch(() => {}), 3000);
  return b;
}

// ---- 한도 1개 × 부팅 1회(또는 tier 재부팅 포함) = 컨테이너 1개 ----
let seq = 0;
async function trial(limitMB, port, maxBoots) {
  const ctr = `memlimit-${process.pid}-${limitMB}-${seq++}`;
  const res = { limitMB, boots: [], infra: null };
  docker(['rm', '-f', ctr]);
  const r = docker(['run', '-d', '--name', ctr, '--memory', `${limitMB}m`, '--memory-swap', `${limitMB}m`, '--cpus', CPUS,
    '--add-host', `${HOST_ALIAS}:host-gateway`, '-p', '127.0.0.1::9223', '-v', `${HERE}:/m:ro`,
    '-e', `HOST_ALIAS=${HOST_ALIAS}`, IMAGE, '/m/container-entry.sh']);
  if (r.status !== 0) { res.infra = 'docker run 실패: ' + (r.stderr || '').slice(0, 200); return res; }
  const cleanup = () => docker(['rm', '-f', ctr]);
  let hostPort = null;
  for (let k = 0; k < 40 && !hostPort; k++) { const o = docker(['port', ctr, '9223/tcp']).stdout.trim(); if (o) hostPort = o.split('\n')[0].split(':').pop(); else await sleep(250); }
  let up = false;
  for (let k = 0; k < 60 && hostPort && !up; k++) { try { if ((await fetch(`http://127.0.0.1:${hostPort}/json/version`)).ok) up = true; } catch { /* 재시도 */ } if (!up) await sleep(500); }
  if (!up) { res.infra = 'CDP 연결 불가: ' + docker(['logs', ctr]).stdout.slice(-300); cleanup(); return res; }
  const state = { disconnected: false };
  let br;
  try {
    br = await chromium.connectOverCDP(`http://127.0.0.1:${hostPort}`, { timeout: 20000 });
    br.on('disconnected', () => { state.disconnected = true; });
    const ctx = await br.newContext(PROFILE.ctx);
    await ctx.addInitScript(`try{Object.defineProperty(Navigator.prototype,'deviceMemory',{get:function(){return undefined},configurable:true})}catch(e){} try{Object.defineProperty(Navigator.prototype,'hardwareConcurrency',{get:function(){return ${PROFILE.cores}},configurable:true})}catch(e){}`);
    await ctx.addInitScript(TTFF_INIT); await ctx.addInitScript(LM_INIT);
    const url = `http://localhost:${port}/`;
    for (let bi = 1; bi <= maxBoots; bi++) {
      if (bi > 1) await sleep(2000);
      const b = await bootOnce(ctx, ctr, url, bi, state);
      res.boots.push(b);
      if (b.alive || state.disconnected) break;
    }
  } catch (e) { res.infra = 'driver: ' + String(e.message).slice(0, 200); }
  const ins = docker(['inspect', '-f', '{{.State.OOMKilled}}|{{.State.ExitCode}}', ctr]).stdout.trim(); res.docker = ins;
  try { await tmo(br && br.close(), 3000); } catch { /* 무시 */ }
  cleanup();
  const last = res.boots[res.boots.length - 1];
  res.ok = !!(last && last.ok);
  res.alive = res.boots.map((b) => b.alive);
  return res;
}

// 한도 통과 = 서로 독립인 컨테이너 2번 모두 첫 부팅 통과
async function level(limitMB, port) {
  const reps = [];
  for (let i = 0; i < 2; i++) {
    const t = await trial(limitMB, port, 1);
    reps.push(t);
    if (t.infra) break;
    if (!t.ok) break; // 한 번 실패하면 이 한도는 실패(2번째 시도 생략)
  }
  const infra = reps.find((r) => r.infra);
  const pass = !infra && reps.length === 2 && reps.every((r) => r.ok);
  console.log(`LEVEL ${limitMB}MB pass=${pass}${infra ? ' INFRA=' + infra.infra : ''} reps=${reps.map((r) => `${r.ok}(ttff=${r.boots[0] && r.boots[0].ttff},oom=${r.docker})`).join(',')}`);
  return { limitMB, pass, infra: infra ? infra.infra : null, reps };
}

const { srv, port } = await startServer();
const result = { image: IMAGE, startMB: START, stepMB: STEP, levels: [], minViableMB: null, firstFailMB: null, twoBoot: [], infraErrors: [] };
try {
  if (SINGLE) {
    const l = await level(+SINGLE, port);
    result.levels.push(l);
    if (l.infra) result.infraErrors.push(l.infra);
  } else {
    let mb = START;
    for (let n = 0; n < MAX_LEVELS && mb >= STEP * 4; n++, mb -= STEP) {
      const l = await level(mb, port);
      result.levels.push(l);
      if (l.infra) { result.infraErrors.push(l.infra); break; }
      if (!l.pass) { result.firstFailMB = mb; break; }
      result.minViableMB = mb;
    }
    // 2-boot tier: 실패 한도와 그 아래 한 단계에서, 첫 부팅이 죽으면 재부팅(tier 1)이 사는지 확인
    if (!result.infraErrors.length && result.firstFailMB) {
      for (const mb of [result.firstFailMB, result.firstFailMB - STEP]) {
        if (mb < STEP * 4) continue;
        const t = await trial(mb, port, 2);
        if (t.infra) { result.infraErrors.push(t.infra); break; }
        const b2 = t.boots[1];
        const entry = { limitMB: mb, boot1Alive: !!(t.boots[0] && t.boots[0].alive), boot2Alive: b2 ? !!b2.alive : null, boot2Tier: b2 && b2.fin ? b2.fin.tier : null };
        result.twoBoot.push(entry);
        console.log(`TWOBOOT ${JSON.stringify(entry)}`);
      }
    }
  }
} finally { srv.close(); }

console.log(`RESULT minViableMB=${result.minViableMB} firstFailMB=${result.firstFailMB} infra=${result.infraErrors.length}`);
if (OUT) fs.writeFileSync(OUT, JSON.stringify(result, null, 2));
process.exit(result.infraErrors.length ? 2 : 0);
