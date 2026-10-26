// perf 실기기 측정 HUD (device-hud 변형 전용). HeavyDeviceHudVariant 가 index.html 의 USER_HEAD 영역에 이 파일을 인라인한다.
// 규칙: ES2017 이하 문법만, 모든 저장소 접근은 try/catch.
// 퍼센트 기호로 감싼 대문자 토큰(플레이스홀더 형태)과 빌드 폴더 경로 리터럴은 쓰지 않는다(빌드 검증기가 오탐한다).
// __AIT_HUD_BUILD_JSON__ 은 빌드 시 {variant, posture, unity, sha} JSON 리터럴로 치환된다.
// 자동 측정: 첫 그리기 뒤 5초(부팅 구간)가 지나면 게임 자동 플레이를 켜고 60초를 잰 뒤, 같은 탭에서 AUTO_RUNS 번까지
// 새로고침해 반복한다. 수신 주소는 저장소에 두지 않는다. 배포 직전에 로컬에서 __AIT_HUD_REPORT_URL__ 을 https 주소로
// 바꾸거나 페이지 쿼리 aitHudReport 로 넘기면 측정마다 결과를 POST 하고, 없으면 클립보드 복사만 쓴다.
(function () {
  'use strict';
  if (window.__aitDeviceHud) return;
  window.__aitDeviceHud = true;

  var BUILD = __AIT_HUD_BUILD_JSON__;
  var LABEL = [BUILD.variant || 'default', BUILD.posture || 'default', BUILD.unity || '?', BUILD.sha || 'nosha'].join('|');
  var STORE_KEY = 'aitDeviceHud.v1:' + LABEL;
  var MAX_RUNS = 10;
  var PHASE_A_MS = 5000;
  var PHASE_B_MS = 60000;
  var AUTO_RUNS = 3;
  var REPORT_URL = (function () {
    var q = safe(function () { return new URLSearchParams(location.search).get('aitHudReport'); }, null);
    var u = q || '__AIT_HUD_REPORT_URL__';
    return /^https:\/\//.test(u) ? u : null;
  })();

  function now() { return performance.now(); }
  function r1(v) { return typeof v === 'number' && isFinite(v) ? Math.round(v * 10) / 10 : null; }
  function safe(fn, dflt) { try { var v = fn(); return v === undefined ? dflt : v; } catch (e) { return dflt; } }

  var run = {
    id: Date.now().toString(36) + Math.floor(Math.random() * 1296).toString(36),
    at: new Date().toISOString(),
    state: 'boot',
    ttff: null,
    overlayHidden: null,
    firstVisible: null
  };

  // ---- 자동 반복 회차 (sessionStorage: 같은 탭의 새로고침에서만 이어진다. QR 로 새로 열면 1회차부터) ----
  var SEQ_KEY = 'aitDeviceHud.seq:' + LABEL;
  var session = safe(function () { return JSON.parse(window.sessionStorage.getItem(SEQ_KEY)); }, null);
  if (!session || typeof session.n !== 'number') session = { id: run.id, n: 0 };
  session.n++;
  safe(function () { window.sessionStorage.setItem(SEQ_KEY, JSON.stringify(session)); });
  run.session = session.id;
  run.seq = session.n;
  run.kind = session.n === 1 ? 'open' : 'reload';

  // ---- wasm Memory 수집 (perf-ttff.test.js 와 같은 방식: 생성자를 감싸고 grow 로 보강) ----
  var memories = [];
  safe(function () {
    if (!(window.WebAssembly && WebAssembly.Memory)) return;
    var OrigMemory = WebAssembly.Memory;
    var Wrapped = function (desc) {
      var m = new OrigMemory(desc);
      try { memories.push(m); } catch (e) {}
      return m;
    };
    Wrapped.prototype = OrigMemory.prototype;
    WebAssembly.Memory = Wrapped;
    var origGrow = OrigMemory.prototype.grow;
    if (typeof origGrow === 'function') {
      OrigMemory.prototype.grow = function () {
        try { if (memories.indexOf(this) < 0) memories.push(this); } catch (e) {}
        return origGrow.apply(this, arguments);
      };
    }
  });

  function wasmHeapMax() {
    var mx = 0;
    for (var i = 0; i < memories.length; i++) {
      try { mx = Math.max(mx, memories[i].buffer.byteLength); } catch (e) {}
    }
    safe(function () {
      var M = window.unityInstance && window.unityInstance.Module;
      if (!M) return;
      if (M.wasmMemory && M.wasmMemory.buffer) mx = Math.max(mx, M.wasmMemory.buffer.byteLength);
      var h = M.HEAPU8 || M.HEAP8;
      if (h && h.buffer) mx = Math.max(mx, h.buffer.byteLength);
    });
    return mx;
  }

  // ---- 프레임당 CPU 작업 시간 ----
  // iOS WebKit 은 rAF 를 60Hz 로 묶어 프레임 간격만으로는 빌드 간 CPU 차이가 가려진다. Unity 메인 루프는 rAF 콜백에서
  // 돌므로, 콜백 실행 시간을 같은 vsync 타임스탬프끼리 합쳐 프레임당 작업 시간으로 잰다(HUD 자신의 콜백은 뺀다).
  var workA = [], workB = [], workTitle = [], workPlay = [];
  var curFrameTs = null, curFrameWork = 0;
  var hudCb = null;
  function flushWork() {
    if (curFrameTs === null || frameT0 === null || frameDone) return;
    var rel = curFrameTs - frameT0;
    if (rel < 0) return;
    if (rel <= PHASE_A_MS) workA.push(curFrameWork);
    else if (rel <= PHASE_A_MS + PHASE_B_MS) {
      workB.push(curFrameWork);
      var gs = window.__AIT_RUN && window.__AIT_RUN.state;
      if (gs === 'playing') workPlay.push(curFrameWork); else workTitle.push(curFrameWork);
    }
  }
  safe(function () {
    var origRaf = window.requestAnimationFrame;
    if (typeof origRaf !== 'function') return;
    window.requestAnimationFrame = function (cb) {
      if (cb === hudCb || typeof cb !== 'function') return origRaf.call(window, cb);
      return origRaf.call(window, function (ts) {
        if (ts !== curFrameTs) { flushWork(); curFrameTs = ts; curFrameWork = 0; }
        var t0 = performance.now();
        try { return cb(ts); } finally { curFrameWork += performance.now() - t0; }
      });
    };
  });

  // ---- wasm 힙 추이 (5초 간격) ----
  var heapSamples = [];
  setInterval(function () {
    if (frameDone || frameT0 === null) return;
    heapSamples.push([Math.round((performance.now() - frameT0) / 1000), wasmHeapMax()]);
  }, 5000);

  // ---- 프레임 수집 ----
  var frameStarted = false;
  var frameT0 = null;
  var lastTs = null;
  var intervalsA = [];
  var intervalsB = [];
  var frameDone = false;
  var hiddenSkips = 0;
  var skipNext = false;

  function onFrame(ts) {
    if (frameDone) return;
    if (frameT0 === null) {
      frameT0 = ts;
      lastTs = ts;
    } else {
      var dt = ts - lastTs;
      lastTs = ts;
      if (skipNext) {
        skipNext = false;
        hiddenSkips++;
      } else {
        var rel = ts - frameT0;
        if (rel <= PHASE_A_MS) intervalsA.push(dt);
        else if (rel <= PHASE_A_MS + PHASE_B_MS) intervalsB.push(dt);
      }
      if (ts - frameT0 > PHASE_A_MS + PHASE_B_MS) {
        frameDone = true;
        run.state = 'done';
        persist();
        onRunDone();
        return;
      }
      if (run.state !== 'frames') run.state = 'frames';
      if (ts - frameT0 > PHASE_A_MS && !run.__aSaved) {
        run.__aSaved = true;
        persist();
        startAutoplay();
        upload(false);
      }
    }
    window.requestAnimationFrame(onFrame);
  }
  hudCb = onFrame;

  function startFrames() {
    if (frameStarted) return;
    frameStarted = true;
    run.state = 'frames';
    if (typeof window.requestAnimationFrame === 'function') window.requestAnimationFrame(onFrame);
  }

  document.addEventListener('visibilitychange', function () {
    if (document.hidden) { skipNext = true; persist(); } else { skipNext = true; }
  });
  window.addEventListener('pagehide', function () { persist(); });

  function stats(arr) {
    var n = arr.length;
    if (!n) return null;
    var s = arr.slice().sort(function (a, b) { return a - b; });
    var sum = 0, g33 = 0, g50 = 0, g100 = 0;
    for (var i = 0; i < n; i++) {
      var v = arr[i];
      sum += v;
      if (v > 33.5) g33++;
      if (v > 50) g50++;
      if (v > 100) g100++;
    }
    function pct(p) { return s[Math.min(n - 1, Math.max(0, Math.ceil(p * n) - 1))]; }
    return {
      n: n,
      spanMs: r1(sum),
      avgFps: sum > 0 ? r1(n / (sum / 1000)) : null,
      p50: r1(pct(0.5)), p95: r1(pct(0.95)), p99: r1(pct(0.99)), max: r1(s[n - 1]),
      gt33: g33, gt50: g50, gt100: g100
    };
  }

  // ---- 첫 프레임 (perf-ttff.test.js 와 같은 정의: 기본 프레임버퍼로의 첫 draw 호출) ----
  safe(function () {
    var origGet = HTMLCanvasElement.prototype.getContext;
    HTMLCanvasElement.prototype.getContext = function (type) {
      var ctx = origGet.apply(this, arguments);
      if (ctx && (type === 'webgl' || type === 'webgl2' || type === 'experimental-webgl')) {
        var methods = ['drawElements', 'drawArrays', 'drawElementsInstanced', 'drawArraysInstanced', 'drawRangeElements'];
        var origs = {};
        // 첫 화면 draw 를 잡으면 원래 함수로 되돌려 플레이 중 draw 호출마다 래퍼 비용이 붙지 않게 한다.
        var restore = function () { for (var k in origs) { try { ctx[k] = origs[k]; } catch (e) {} } };
        for (var i = 0; i < methods.length; i++) {
          (function (m) {
            if (typeof ctx[m] === 'function' && !ctx['__hud_' + m]) {
              var orig = ctx[m];
              origs[m] = orig;
              ctx[m] = function () {
                if (run.ttff === null) {
                  var onScreen = true;
                  try { onScreen = ctx.getParameter(ctx.FRAMEBUFFER_BINDING) === null; } catch (e) {}
                  if (onScreen) {
                    run.ttff = now();
                    updateFirstVisible();
                    startFrames();
                  }
                }
                var r = orig.apply(ctx, arguments);
                if (run.ttff !== null) restore();
                return r;
              };
              ctx['__hud_' + m] = true;
            }
          })(methods[i]);
        }
      }
      return ctx;
    };
  });

  // ---- 오버레이 숨김 시점 (#ait-loading-wrapper 가 display:none 이 되는 시각) ----
  function updateFirstVisible() {
    if (typeof run.ttff === 'number' && typeof run.overlayHidden === 'number') {
      run.firstVisible = Math.max(run.ttff, run.overlayHidden);
    }
  }
  function watchOverlay() {
    safe(function () {
      var el = document.querySelector('#ait-loading-wrapper');
      if (!el) return;
      var check = function () {
        if (run.overlayHidden === null && el.style.display === 'none') {
          run.overlayHidden = now();
          updateFirstVisible();
        }
      };
      new MutationObserver(check).observe(el, { attributes: true, attributeFilter: ['style'] });
      check();
    });
  }

  // ---- longtask (미지원이면 null) ----
  var longtask = null;
  safe(function () {
    if (typeof PerformanceObserver !== 'function') return;
    var types = PerformanceObserver.supportedEntryTypes;
    if (!types || types.indexOf('longtask') < 0) return;
    longtask = { count: 0, totalMs: 0, maxMs: 0 };
    var po = new PerformanceObserver(function (list) {
      var es = list.getEntries();
      for (var i = 0; i < es.length; i++) {
        longtask.count++;
        longtask.totalMs += es[i].duration;
        if (es[i].duration > longtask.maxMs) longtask.maxMs = es[i].duration;
      }
    });
    po.observe({ type: 'longtask', buffered: true });
  });

  // ---- 스냅샷 ----
  function collectNav() {
    return safe(function () {
      var n = performance.getEntriesByType('navigation')[0];
      if (n) {
        return {
          type: n.type, responseEnd: r1(n.responseEnd), domInteractive: r1(n.domInteractive),
          domContentLoaded: r1(n.domContentLoadedEventEnd), load: r1(n.loadEventEnd) || null,
          transferSize: n.transferSize, encodedBodySize: n.encodedBodySize, decodedBodySize: n.decodedBodySize
        };
      }
      var t = performance.timing;
      return {
        type: 'legacy', responseEnd: t.responseEnd - t.navigationStart,
        domContentLoaded: t.domContentLoadedEventEnd - t.navigationStart, load: t.loadEventEnd ? t.loadEventEnd - t.navigationStart : null
      };
    }, null);
  }

  function collectMarks() {
    return safe(function () {
      var out = {};
      var ms = performance.getEntriesByType('mark');
      for (var i = 0; i < ms.length; i++) {
        if (ms[i].name.indexOf('ait:') === 0 && !(ms[i].name in out)) out[ms[i].name] = r1(ms[i].startTime);
      }
      return out;
    }, {});
  }

  function collectResources() {
    return safe(function () {
      var out = [];
      var es = performance.getEntriesByType('resource');
      for (var i = 0; i < es.length && out.length < 16; i++) {
        var e = es[i];
        var path = String(e.name).split('?')[0].split('#')[0];
        var base = path.substring(path.lastIndexOf('/') + 1);
        var lower = base.toLowerCase();
        if (!/(\.wasm|\.data|framework|loader|\.unityweb|\.br$|\.gz$)/.test(lower)) continue;
        out.push({
          name: base, init: e.initiatorType, start: r1(e.startTime), end: r1(e.responseEnd),
          transfer: e.transferSize, encoded: e.encodedBodySize, decoded: e.decodedBodySize
        });
      }
      return out;
    }, []);
  }

  function collectEnv() {
    var mem = window.AITMemory || {};
    var pm = safe(function () {
      var m = performance.memory;
      return m ? { used: m.usedJSHeapSize, total: m.totalJSHeapSize, limit: m.jsHeapSizeLimit } : null;
    }, null);
    var wp = safe(function () {
      var w = window.__AIT_WASM_PATH;
      return w ? JSON.parse(JSON.stringify(w)) : null;
    }, null);
    return {
      ua: navigator.userAgent,
      deviceMemory: safe(function () { return navigator.deviceMemory; }, null),
      cores: safe(function () { return navigator.hardwareConcurrency; }, null),
      dpr: window.devicePixelRatio,
      effectiveDpr: safe(function () { return window.__AIT_EFFECTIVE_DPR; }, null),
      screen: safe(function () { return screen.width + 'x' + screen.height; }, null),
      inner: window.innerWidth + 'x' + window.innerHeight,
      conn: safe(function () {
        var c = navigator.connection;
        return c ? { type: c.effectiveType, rtt: c.rtt, down: c.downlink } : null;
      }, null),
      aitMemory: {
        tier: safe(function () { return mem.lowMemTier; }, null),
        lowRamDevice: safe(function () { return mem.lowRamDevice; }, null),
        lowMemReason: safe(function () { return mem.lowMemReason; }, null),
        prevBoot: safe(function () { return mem.prevBoot; }, null),
        crashCount: safe(function () { return mem.crashCount; }, null),
        bootFailCount: safe(function () { return mem.bootFailCount; }, null),
        lateDeath: safe(function () { return mem.lateDeath; }, null)
      },
      wasmPath: wp,
      firstInteractiveLog: safe(function () { return window.__AIT_FIRST_INTERACTIVE_LOG; }, null),
      wasmHeapMaxBytes: wasmHeapMax(),
      jsHeap: pm
    };
  }

  // ---- 게임 자동 플레이 (mobilegame posture 의 AITRunScene.Cmd: autoplay/start/retry, 상태는 window.__AIT_RUN) ----
  var autoTimer = null, autoStarts = 0;
  function gameCmd(c) {
    return safe(function () { window.unityInstance.SendMessage('AITRunScene', 'Cmd', c); return true; }, false);
  }
  function startAutoplay() {
    if (autoTimer) return;
    var statsReset = false;
    autoTimer = setInterval(function () {
      if (frameDone) { clearInterval(autoTimer); return; }
      var s = window.__AIT_RUN;
      if (!s) return;
      // 게임 쪽 프레임 통계를 자동 플레이 구간만으로 다시 센다.
      if (!statsReset) statsReset = gameCmd('stats');
      if (s.state === 'title' || s.state === 'over') {
        gameCmd('autoplay');
        if (gameCmd(s.state === 'title' ? 'start' : 'retry')) autoStarts++;
      }
    }, 500);
  }
  function gameState() {
    return safe(function () {
      var s = window.__AIT_RUN;
      if (!s) return null;
      var keep = ['state', 'step', 'score', 'runs', 'autoplay', 'checksum', 'checksumAt', 'frames', 'avgMs', 'p50Ms', 'p95Ms',
        'p99Ms', 'maxMs', 'longFrames', 'heavy', 'stage', 'stageLoads', 'stageLoadMsMax', 'stageErrors', 'bodies', 'particles',
        'buildings', 'screenW', 'screenH', 'errorCount', 'bgmPlaying'];
      var o = {};
      for (var i = 0; i < keep.length; i++) if (keep[i] in s) o[keep[i]] = s[keep[i]];
      o.autoStarts = autoStarts;
      return o;
    }, null);
  }

  // ---- 자동 전송 + 다음 회차 ----
  var uploadState = REPORT_URL ? 'wait' : 'off';
  function upload(final) {
    if (!REPORT_URL) return;
    var body;
    try {
      body = JSON.stringify({ build: LABEL, buildInfo: BUILD, final: final, sentAt: new Date().toISOString(), current: snapshot() });
    } catch (e) { return; }
    var tries = 0;
    var send = function () {
      tries++;
      // text/plain 이면 CORS 단순 요청이라 preflight 가 없다.
      safe(function () {
        fetch(REPORT_URL, { method: 'POST', mode: 'cors', keepalive: body.length < 60000, headers: { 'Content-Type': 'text/plain' }, body: body })
          .then(function (r) {
            if (!r.ok) throw new Error('http ' + r.status);
            if (final) { uploadState = 'sent'; refreshLabel(); }
          })
          .catch(function () {
            if (tries < 5) setTimeout(send, 1500 * tries);
            else if (final) { uploadState = 'fail'; refreshLabel(); }
          });
      });
    };
    send();
  }
  function onRunDone() {
    upload(true);
    if (run.seq >= AUTO_RUNS) return;
    banner(run.seq + '/' + AUTO_RUNS + ' 회차 측정 완료\n결과를 보내고 ' + (run.seq + 1) + '회차를 위해 새로고침합니다');
    // 전송이 끝나거나 실패로 확정될 때까지 기다린 뒤 새로고침한다(최대 15초).
    var waited = 0;
    var t = setInterval(function () {
      waited += 500;
      if (waited >= 2500 && (uploadState === 'sent' || uploadState === 'fail' || uploadState === 'off' || waited >= 15000)) {
        clearInterval(t);
        safe(function () { location.reload(); });
      }
    }, 500);
  }

  function snapshot() {
    var out = {
      id: run.id, at: run.at, state: run.state, elapsedMs: r1(now()),
      session: run.session, seq: run.seq, kind: run.kind,
      timeOrigin: safe(function () { return Math.round(performance.timeOrigin); }, null),
      nav: collectNav(),
      ttff: r1(run.ttff), overlayHidden: r1(run.overlayHidden), firstVisible: r1(run.firstVisible),
      marks: collectMarks(),
      resources: collectResources(),
      env: collectEnv(),
      frames: {
        a: stats(intervalsA), b: stats(intervalsB),
        sinceFirstDrawMs: frameT0 === null ? null : r1(lastTs - frameT0),
        hiddenSkips: hiddenSkips
      },
      work: { a: stats(workA), b: stats(workB), title: stats(workTitle), play: stats(workPlay) },
      heapSamples: heapSamples,
      longtask: longtask ? { count: longtask.count, totalMs: r1(longtask.totalMs), maxMs: r1(longtask.maxMs) } : null,
      game: gameState()
    };
    return out;
  }

  // ---- 저장 (실행마다 1건, 최대 MAX_RUNS 건) ----
  var memStore = null;
  function readStore() {
    try {
      var raw = window.localStorage.getItem(STORE_KEY);
      if (raw) {
        var p = JSON.parse(raw);
        if (p && p.runs && p.runs.length !== undefined) return p;
      }
    } catch (e) {}
    return memStore || { runs: [] };
  }
  function writeStore(st) {
    memStore = st;
    try { window.localStorage.setItem(STORE_KEY, JSON.stringify(st)); } catch (e) {}
  }
  function persist() {
    try {
      var st = readStore();
      var snap = snapshot();
      var replaced = false;
      for (var i = 0; i < st.runs.length; i++) {
        if (st.runs[i].id === run.id) { st.runs[i] = snap; replaced = true; break; }
      }
      if (!replaced) st.runs.push(snap);
      while (st.runs.length > MAX_RUNS) st.runs.shift();
      writeStore(st);
    } catch (e) {}
    refreshLabel();
  }

  function buildPayload() {
    var st = readStore();
    var cur = snapshot();
    var hist = [];
    for (var i = st.runs.length - 1; i >= 0; i--) {
      if (st.runs[i].id !== run.id) hist.push(st.runs[i]);
    }
    var payload = {
      build: LABEL,
      buildInfo: BUILD,
      copiedAt: new Date().toISOString(),
      runCount: hist.length + 1,
      current: cur,
      history: hist
    };
    return JSON.stringify(payload);
  }

  // ---- UI ----
  var box = null, btn = null, clr = null, labelTimer = null, clearTimer = null, clearArmed = false;

  function storedCount() {
    var st = readStore();
    var c = st.runs.length;
    var found = false;
    for (var i = 0; i < st.runs.length; i++) if (st.runs[i].id === run.id) found = true;
    return found ? c : c + 1;
  }
  function progress() {
    if (run.state === 'done') {
      if (run.seq < AUTO_RUNS) return '⏭ ' + run.seq + '/' + AUTO_RUNS + ' 다음 회차';
      return uploadState === 'sent' ? '✅ ' + AUTO_RUNS + '/' + AUTO_RUNS + ' 전송 완료' :
        uploadState === 'fail' ? '⚠️ 전송 실패' : '✅ ' + AUTO_RUNS + '/' + AUTO_RUNS + ' 완료';
    }
    var sec = frameT0 === null || lastTs === null ? 0 : Math.floor((lastTs - frameT0) / 1000);
    return '⏱ ' + run.seq + '/' + AUTO_RUNS + ' · ' + Math.min(sec, (PHASE_A_MS + PHASE_B_MS) / 1000) + 's';
  }
  function baseLabel() { return progress() + ' · 📋 ' + storedCount(); }
  function refreshLabel() { if (btn && !labelTimer) btn.textContent = baseLabel(); }
  function flash(text) {
    if (!btn) return;
    btn.textContent = text;
    if (labelTimer) clearTimeout(labelTimer);
    labelTimer = setTimeout(function () { labelTimer = null; refreshLabel(); }, 1600);
  }

  function banner(text) {
    safe(function () {
      var b = document.createElement('div');
      b.textContent = text;
      b.style.cssText = 'position:fixed;left:8%;right:8%;top:40%;z-index:2147483647;padding:20px;border-radius:14px;' +
        'background:rgba(0,0,0,.82);color:#fff;font:600 17px/1.5 sans-serif;text-align:center;white-space:pre-line;pointer-events:none;';
      document.body.appendChild(b);
    });
  }

  function legacyCopy(text) {
    var ta = document.createElement('textarea');
    ta.value = text;
    ta.setAttribute('readonly', '');
    ta.style.cssText = 'position:fixed;left:0;top:0;width:1px;height:1px;opacity:0;';
    document.body.appendChild(ta);
    var ok = false;
    try {
      ta.focus();
      ta.select();
      ta.setSelectionRange(0, text.length);
      ok = document.execCommand('copy');
    } catch (e) { ok = false; }
    document.body.removeChild(ta);
    return ok;
  }

  function showManual(text) {
    var wrap = document.createElement('div');
    wrap.style.cssText = 'position:fixed;left:0;top:0;right:0;bottom:0;z-index:2147483647;background:rgba(0,0,0,.85);' +
      'display:flex;flex-direction:column;padding:calc(env(safe-area-inset-top,0px) + 12px) 12px 12px;box-sizing:border-box;pointer-events:auto;';
    var msg = document.createElement('div');
    msg.textContent = '자동 복사에 실패했습니다. 아래 텍스트를 길게 눌러 전체 선택 후 복사해 주세요.';
    msg.style.cssText = 'color:#fff;font:14px/1.4 sans-serif;margin-bottom:8px;';
    var ta = document.createElement('textarea');
    ta.value = text;
    ta.style.cssText = 'flex:1;width:100%;box-sizing:border-box;font:11px/1.3 monospace;';
    var close = document.createElement('button');
    close.textContent = '닫기';
    close.style.cssText = 'margin-top:8px;padding:12px;font:16px sans-serif;';
    close.addEventListener('click', function () { document.body.removeChild(wrap); });
    wrap.appendChild(msg);
    wrap.appendChild(ta);
    wrap.appendChild(close);
    document.body.appendChild(wrap);
    ta.focus();
    ta.select();
    try { ta.setSelectionRange(0, text.length); } catch (e) {}
  }

  function onCopy() {
    persist();
    var text;
    try { text = buildPayload(); } catch (e) { flash('⚠️ 실패'); return; }
    var fallback = function () {
      if (legacyCopy(text)) { flash('✅ 복사됨'); return; }
      flash('⚠️ 수동 복사');
      showManual(text);
    };
    var done = false;
    try {
      if (navigator.clipboard && typeof navigator.clipboard.writeText === 'function') {
        navigator.clipboard.writeText(text).then(function () {
          done = true;
          flash('✅ 복사됨');
        }, function () { if (!done) fallback(); });
        return;
      }
    } catch (e) {}
    fallback();
  }

  function onClear() {
    if (!clearArmed) {
      clearArmed = true;
      clr.textContent = '한 번 더';
      clearTimer = setTimeout(function () { clearArmed = false; clr.textContent = '🗑'; }, 3000);
      return;
    }
    clearArmed = false;
    if (clearTimer) clearTimeout(clearTimer);
    clr.textContent = '🗑';
    try { window.localStorage.removeItem(STORE_KEY); } catch (e) {}
    memStore = null;
    persist();
    flash('초기화됨');
  }

  function mountUi() {
    if (box || !document.body) return;
    box = document.createElement('div');
    box.setAttribute('data-ait-device-hud', '1');
    box.style.cssText = 'position:fixed;z-index:2147483646;top:calc(env(safe-area-inset-top,0px) + 6px);' +
      'right:calc(env(safe-area-inset-right,0px) + 6px);display:flex;gap:4px;pointer-events:none;opacity:.55;';
    var css = 'pointer-events:auto;border:0;border-radius:10px;padding:6px 9px;font:12px/1.2 sans-serif;' +
      'color:#fff;background:rgba(0,0,0,.6);-webkit-tap-highlight-color:transparent;touch-action:manipulation;';
    btn = document.createElement('button');
    btn.type = 'button';
    btn.style.cssText = css;
    btn.addEventListener('click', onCopy);
    clr = document.createElement('button');
    clr.type = 'button';
    clr.textContent = '🗑';
    clr.style.cssText = css;
    clr.addEventListener('click', onClear);
    box.appendChild(btn);
    box.appendChild(clr);
    document.body.appendChild(box);
    refreshLabel();
  }

  function onReady() {
    watchOverlay();
    persist();
    mountUi();
  }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', onReady);
  else onReady();

  // 부팅 중 앱이 죽어도 마지막 값이 남도록 주기 저장(가볍게 3초).
  setInterval(function () { if (!frameDone) persist(); }, 3000);
  setInterval(refreshLabel, 1000);
})();
