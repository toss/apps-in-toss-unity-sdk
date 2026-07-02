/*!
 * AIT Dev Console — Apps in Toss Unity SDK 개발용 디버그 콘솔 (vConsole 통합)
 *
 * dev 빌드에서만 로드됨(enableDebugConsole). window.VConsole(vconsole.min.js) 의존.
 * - vConsole 초기화: Log / Network / Storage / System 패널 + 플로팅 스위치 버튼
 * - 커스텀 "Metrics" 플러그인 등록: 기존 자체 콘솔의 Metrics 탭을 이식
 *     Events / Statistics / Performance(FPS·JS Heap·GC·frame stall)
 *     → window.AppsInToss.eventLog/debugLog 인터셉트로 공급
 * - window._aitEarlyLogs(브릿지 부팅 로그) 리플레이
 * - Unity WebGL 전역 키보드 캡처 우회(vConsole 입력창 타이핑 허용)
 *
 * 이 파일은 SDK 소유 코드다. 동일 폴더의 vconsole.min.js는 vendored(MIT, Tencent).
 */
(function () {
  'use strict';

  if (!window.VConsole) {
    console.error('[AIT] vConsole 미로드 — Dev Console 초기화를 건너뜁니다.');
    return;
  }
  if (window._aitVConsole) {
    return; // 중복 초기화 방지
  }

  // =========================================================================
  // 콘솔 로그 미러 (Logs 복사용)
  //  vConsole Log 탭은 가상 스크롤 + 중첩 객체 지연 렌더라 DOM 스크래핑 시
  //  화면 밖/미확장 항목이 누락된다. 그래서 자체 링버퍼에 console.* 를 미러링한다.
  //  "wrap, don't replace" — 원본 메서드를 항상 통과 호출해 vConsole 자체 캡처는 그대로 동작.
  //  vConsole 생성보다 먼저 설치해 초기(replayEarlyLogs 포함) 로그도 담는다.
  // =========================================================================
  var logMirror = [];
  var LOG_MIRROR_MAX = 500;
  var LOG_ENTRY_MAX_CHARS = 2000;

  // JSON.stringify 는 자르기 전에 입력 전체를 직렬화하므로 대형 객체(typed array 등)에서
  // 수백 ms 동기 스톨을 일으킨다 — frame_stall 지표를 콘솔 미러 스스로 오염시키는 자기유발 잼.
  // 예산(budget.left) 안에서만 걷는 직렬화로 상한을 O(LOG_ENTRY_MAX_CHARS)로 고정한다.
  // depth 상한 덕에 순환 참조도 안전(JSON.stringify 는 throw 했음).
  function boundedStringify(value, budget, depth) {
    if (value === null) return 'null';
    var t = typeof value;
    if (t === 'string') {
      var s = value.length > budget.left ? value.slice(0, Math.max(0, budget.left)) + '…' : value;
      budget.left -= s.length + 2;
      return JSON.stringify(s);
    }
    if (t === 'number' || t === 'boolean') { var p = String(value); budget.left -= p.length; return p; }
    if (t === 'undefined') { budget.left -= 9; return 'undefined'; }
    if (t === 'function') { budget.left -= 10; return '[Function]'; }
    if (t !== 'object') { var o = String(value); budget.left -= o.length; return o; } // symbol/bigint
    if (typeof ArrayBuffer !== 'undefined' && ArrayBuffer.isView(value)) {
      // typed array / DataView: 원소를 걷지 않고 요약 (JSON.stringify 는 인덱스 전부를 키로 순회)
      var name = (value.constructor && value.constructor.name) || 'TypedArray';
      var sum = '[' + name + '(' + (value.length != null ? value.length : value.byteLength) + ')]';
      budget.left -= sum.length;
      return sum;
    }
    if (depth >= 4 || budget.left <= 0) return '…';
    var i, out;
    if (Object.prototype.toString.call(value) === '[object Array]') {
      out = [];
      for (i = 0; i < value.length; i++) {
        if (budget.left <= 0) { out.push('…'); break; }
        out.push(boundedStringify(value[i], budget, depth + 1));
      }
      return '[' + out.join(',') + ']';
    }
    var keys;
    try { keys = Object.keys(value); } catch (e) { return '[object]'; }
    out = [];
    for (i = 0; i < keys.length; i++) {
      if (budget.left <= 0) { out.push('…'); break; }
      var v;
      try { v = value[keys[i]]; } catch (e2) { v = '[getter threw]'; }
      budget.left -= keys[i].length + 3;
      out.push(JSON.stringify(keys[i]) + ':' + boundedStringify(v, budget, depth + 1));
    }
    return '{' + out.join(',') + '}';
  }

  function safeStringifyArg(arg) {
    if (typeof arg === 'string') return arg;
    if (arg === null) return 'null';
    if (arg === undefined) return 'undefined';
    if (arg instanceof Error) return arg.stack || (arg.name + ': ' + arg.message);
    if (typeof arg === 'object') {
      try { return boundedStringify(arg, { left: LOG_ENTRY_MAX_CHARS }, 0); } catch (e) { return String(arg); }
    }
    return String(arg);
  }

  (function installConsoleMirror() {
    ['log', 'info', 'warn', 'error', 'debug'].forEach(function (level) {
      var orig = console[level];
      if (typeof orig !== 'function') return;
      try {
        // 일부 웹뷰/셸이 console 메서드를 non-writable 로 잠글 수 있다(strict mode 에선 throw).
        // 재할당 실패는 해당 레벨의 미러링만 포기하고 나머지 초기화는 계속 진행.
        console[level] = function () {
          try {
            var parts = [];
            for (var i = 0; i < arguments.length; i++) parts.push(safeStringifyArg(arguments[i]));
            var text = parts.join(' ');
            if (text.length > LOG_ENTRY_MAX_CHARS) text = text.slice(0, LOG_ENTRY_MAX_CHARS) + '…(truncated)';
            logMirror.push({ ts: Date.now(), level: level, text: text });
            if (logMirror.length > LOG_MIRROR_MAX) logMirror.shift();
          } catch (e) { /* 미러링 실패는 무시 — 원본 로깅은 계속 */ }
          return orig.apply(console, arguments);
        };
      } catch (eAssign) { /* console[level] 잠김 — 이 레벨은 원본 유지 */ }
    });
  })();

  // =========================================================================
  // Metrics 상태 (콘솔 UI 표시 여부와 무관하게 이벤트를 버퍼링)
  // =========================================================================
  var metricEvents = [];
  var metricEventCounts = {};
  var MAX_METRIC_EVENTS = 200;
  var sessionStartTime = Date.now();
  var activeFilter = 'all';
  var currentSubtab = 'events';
  var isMetricsVisible = false;
  var metricsWired = false;

  var categoryMeta = {
    scene: { cssClass: 'scene' },
    error: { cssClass: 'error' },
    gc: { cssClass: 'gc' },
    lifecycle: { cssClass: 'lifecycle' },
    frame_stall: { cssClass: 'frame_stall' },
    screen: { cssClass: 'screen' },
    timescale: { cssClass: 'timescale' },
    low_memory: { cssClass: 'low_memory' }
  };
  var allCategories = ['scene', 'error', 'gc', 'lifecycle', 'frame_stall', 'screen', 'timescale', 'low_memory'];

  function getCategoryFromLogName(logName) {
    if (!logName) return 'unknown';
    for (var i = 0; i < allCategories.length; i++) {
      if (logName.indexOf(allCategories[i]) !== -1) return allCategories[i];
    }
    return 'unknown';
  }

  function formatEventSummary(event) {
    var p = event.params;
    var cat = event.category;
    if (cat === 'gc' && p) {
      return 'Gen' + (p.generation || 0) + ' collection (total: ' + (p.gc_count || '?') + ')';
    }
    if (cat === 'frame_stall' && p) {
      return (p.stall_duration_ms || '?') + 'ms frame (threshold: ' + (p.threshold_ms || '?') + 'ms)';
    }
    if (cat === 'lifecycle' && p) {
      return 'focus: ' + (p.has_focus !== undefined ? p.has_focus : '?');
    }
    if (cat === 'error' && p) {
      var msg = p.error_message || p.message || '';
      return msg.length > 60 ? msg.substring(0, 60) + '...' : msg;
    }
    if (cat === 'scene' && p) {
      return 'Loaded: ' + (p.scene_name || '?') + ' (' + (p.load_mode || '?') + ')';
    }
    if (cat === 'screen' && p) {
      return (p.width || '?') + 'x' + (p.height || '?') + ' orientation: ' + (p.orientation || '?');
    }
    if (cat === 'timescale' && p) {
      return 'timeScale: ' + (p.time_scale !== undefined ? p.time_scale : '?');
    }
    if (cat === 'low_memory') {
      return 'Low memory warning';
    }
    return event.logName || '';
  }

  function captureMetricEvent(params) {
    if (!params) return;
    var logName = params.log_name || params.Log_name || '';
    var category = getCategoryFromLogName(logName);
    var parsedParams = params.params || params.Params;
    if (typeof parsedParams === 'string') {
      try { parsedParams = JSON.parse(parsedParams); } catch (e) { parsedParams = {}; }
    }
    parsedParams = parsedParams || {};

    var event = {
      timestamp: Date.now(),
      logName: logName,
      logType: params.log_type || params.Log_type || '',
      category: category,
      params: parsedParams
    };

    metricEvents.push(event);
    if (metricEvents.length > MAX_METRIC_EVENTS) metricEvents.shift();

    metricEventCounts[category] = (metricEventCounts[category] || 0) + 1;

    if (isMetricsVisible) updateMetricExplorerUI();
  }

  // =========================================================================
  // window.AppsInToss.eventLog / debugLog 인터셉트
  //  - ES module namespace는 프로퍼티가 non-configurable이라 Proxy로 대체 불가.
  //    일반 객체로 shallow copy하여 eventLog/debugLog만 래핑하고,
  //    window.AppsInToss를 getter/setter로 교체해 향후 재할당도 대응.
  //  - 이 스크립트가 unity-bridge.ts(AppsInToss 할당) 전/후 어느 쪽에 로드돼도 동작.
  // =========================================================================
  (function setupEventLogInterception() {
    function wrapTarget(target) {
      if (!target || typeof target !== 'object') return;
      var origEventLog = target.eventLog;
      if (typeof origEventLog !== 'function') return;

      var wrapper = {};
      var keys = Object.keys(target);
      // 함수 위임 래퍼는 키당 1회 생성해 캐시한다 — 매 read마다 새 클로저를 만들면
      // === 동일성이 깨지고(핸들러 해제 패턴 등) 프로퍼티 접근마다 GC 압박이 생긴다.
      // 또한 createPermissionFunction 이 함수에 부착하는 own property
      // (getPermission/openPermissionDialog 등)를 복사해 프로덕션(비래핑) 표면과 맞춘다.
      var fnCache = {};
      function delegateFn(key, v) {
        var cached = fnCache[key];
        if (cached && cached._aitOrig === v) return cached;
        var bound = function () { return v.apply(target, arguments); };
        try {
          Object.keys(v).forEach(function (k) { bound[k] = v[k]; });
          Object.defineProperty(bound, 'name', { value: v.name, configurable: true });
          Object.defineProperty(bound, '_aitOrig', { value: v, configurable: true });
        } catch (e) { bound._aitOrig = v; /* 표면 복사 실패해도 호출 위임은 유지 */ }
        fnCache[key] = bound;
        return bound;
      }
      keys.forEach(function (key) {
        if (key === 'eventLog' || key === 'debugLog') return; // 별도 처리
        Object.defineProperty(wrapper, key, {
          get: function () {
            var v = target[key];
            // 함수 값은 원본 target을 this로 바인딩해 위임한다(eventLog/debugLog와 동일 원칙).
            // 그래야 콘솔 활성 시에도 window.AppsInToss.setClipboardText 같은 최상위 SDK 메서드가
            // 프로덕션(비래핑)과 동일한 수신자(this=실제 네임스페이스)로 호출된다.
            // 비함수 값(중첩 네임스페이스 객체 등)은 참조 그대로 반환 → 다음 홉은 실제 객체가 수신자.
            return (typeof v === 'function') ? delegateFn(key, v) : v;
          },
          enumerable: true,
          configurable: true
        });
      });
      wrapper.eventLog = function (params) {
        captureMetricEvent(params);
        return origEventLog.apply(target, arguments);
      };
      var origDebugLog = target.debugLog;
      wrapper.debugLog = function (params) {
        captureMetricEvent(params);
        var fn = origDebugLog || target.debugLog;
        if (typeof fn === 'function') {
          return fn.apply(target, arguments);
        }
      };

      return wrapper;
    }

    var real = window.AppsInToss;
    if (real) {
      var wrapped = wrapTarget(real);
      if (wrapped) {
        try {
          Object.defineProperty(window, 'AppsInToss', {
            configurable: true, enumerable: true,
            get: function () { return wrapped; },
            set: function (val) {
              real = val;
              wrapped = wrapTarget(val) || val;
            }
          });
        } catch (e) {
          window.AppsInToss = wrapped;
        }
      }
    } else {
      try {
        var _val;
        Object.defineProperty(window, 'AppsInToss', {
          configurable: true, enumerable: true,
          get: function () { return _val; },
          set: function (val) {
            _val = wrapTarget(val) || val;
          }
        });
      } catch (e) {
        var pollId = setInterval(function () {
          if (window.AppsInToss) {
            clearInterval(pollId);
            var w = wrapTarget(window.AppsInToss);
            if (w) {
              try { window.AppsInToss = w; } catch (e2) { /* read-only */ }
            }
          }
        }, 300);
      }
    }
  })();

  // =========================================================================
  // Performance 샘플러 (패널 열림 여부와 무관하게 항상 구동 → 히스토리 축적)
  // =========================================================================
  var fpsHistory = [];
  var FPS_HISTORY_MAX = 60;
  var lastFrameTime = performance.now();
  var frameCount = 0;
  var currentFps = 0;

  function measureFps(now) {
    frameCount++;
    var delta = now - lastFrameTime;
    if (delta >= 1000) {
      currentFps = Math.round((frameCount / delta) * 1000 * 10) / 10;
      fpsHistory.push(currentFps);
      if (fpsHistory.length > FPS_HISTORY_MAX) fpsHistory.shift();
      frameCount = 0;
      lastFrameTime = now;
      if (isMetricsVisible && currentSubtab === 'perf') renderPerformance();
    }
    requestAnimationFrame(measureFps);
  }
  requestAnimationFrame(measureFps);

  // =========================================================================
  // Metrics 탭 렌더러
  // =========================================================================
  var METRICS_TAB_HTML =
    '<div id="ait-metrics-root" class="ait-metrics">' +
      '<div class="metric-subtabs">' +
        '<button class="metric-subtab active" data-subtab="events">📝 Events</button>' +
        '<button class="metric-subtab" data-subtab="stats">📈 Statistics</button>' +
        '<button class="metric-subtab" data-subtab="perf">⚡ Performance</button>' +
        '<button id="metric-copy-btn" class="metric-tool-btn" data-label="📋 Copy" title="현재 탭 내용을 클립보드로 복사">📋 Copy</button>' +
        '<button id="metric-copy-logs-btn" class="metric-tool-btn" data-label="🪵 Logs" title="콘솔 로그를 클립보드로 복사">🪵 Logs</button>' +
      '</div>' +
      '<div class="metric-content" id="metric-panel-events">' +
        '<div class="metric-filter" id="metric-filter-bar"></div>' +
        '<div id="metric-event-list"></div>' +
      '</div>' +
      '<div class="metric-content" id="metric-panel-stats" style="display:none;"></div>' +
      '<div class="metric-content" id="metric-panel-perf" style="display:none;"></div>' +
    '</div>';

  var metricPanels = null;
  var eventListEl = null;

  function ensureWired() {
    if (metricsWired) return true;
    var root = document.getElementById('ait-metrics-root');
    if (!root) return false; // 아직 렌더 전

    metricPanels = {
      events: document.getElementById('metric-panel-events'),
      stats: document.getElementById('metric-panel-stats'),
      perf: document.getElementById('metric-panel-perf')
    };
    eventListEl = document.getElementById('metric-event-list');

    // 서브탭 전환
    var subtabBtns = root.querySelectorAll('.metric-subtab');
    subtabBtns.forEach(function (btn) {
      btn.onclick = function () {
        currentSubtab = btn.getAttribute('data-subtab');
        subtabBtns.forEach(function (b) { b.classList.remove('active'); });
        btn.classList.add('active');
        Object.keys(metricPanels).forEach(function (key) {
          metricPanels[key].style.display = key === currentSubtab ? '' : 'none';
        });
        updateMetricExplorerUI();
      };
    });

    // 카테고리 필터 바
    var filterBar = document.getElementById('metric-filter-bar');
    var filterHTML = '<button class="metric-filter-btn active" data-filter="all">All</button>';
    allCategories.forEach(function (cat) {
      filterHTML += '<button class="metric-filter-btn" data-filter="' + cat + '">' + cat + '</button>';
    });
    filterBar.innerHTML = filterHTML;
    filterBar.querySelectorAll('.metric-filter-btn').forEach(function (btn) {
      btn.onclick = function () {
        activeFilter = btn.getAttribute('data-filter');
        filterBar.querySelectorAll('.metric-filter-btn').forEach(function (b) { b.classList.remove('active'); });
        btn.classList.add('active');
        renderEventList();
      };
    });

    // 복사 버튼: "Copy"는 현재 서브탭(events/stats/perf)을 맥락에 맞게, "Logs"는 콘솔 미러를 복사.
    var copyBtn = document.getElementById('metric-copy-btn');
    if (copyBtn) {
      copyBtn.onclick = function () {
        if (currentSubtab === 'stats') {
          handleCopy(copyBtn, buildStatsCopyText(), null);
        } else if (currentSubtab === 'perf') {
          handleCopy(copyBtn, buildPerfCopyText(), null);
        } else {
          handleCopy(copyBtn, buildEventsCopyText(), getFilteredEvents().length);
        }
      };
    }
    var copyLogsBtn = document.getElementById('metric-copy-logs-btn');
    if (copyLogsBtn) {
      copyLogsBtn.onclick = function () {
        handleCopy(copyLogsBtn, buildLogsCopyText(), logMirror.length);
      };
    }

    metricsWired = true;
    return true;
  }

  function renderEventList() {
    if (!eventListEl) return;
    var filtered = activeFilter === 'all'
      ? metricEvents
      : metricEvents.filter(function (e) { return e.category === activeFilter; });

    if (filtered.length === 0) {
      eventListEl.innerHTML = '<div class="metric-no-events">' +
        (metricEvents.length === 0
          ? 'No events captured yet.<br>Events will appear here as the SDK logs them.'
          : 'No events matching filter "' + activeFilter + '"') +
        '</div>';
      return;
    }

    var html = '';
    for (var i = filtered.length - 1; i >= 0; i--) {
      var ev = filtered[i];
      var d = new Date(ev.timestamp);
      var ts = d.getHours().toString().padStart(2, '0') + ':' +
               d.getMinutes().toString().padStart(2, '0') + ':' +
               d.getSeconds().toString().padStart(2, '0');
      var cssClass = categoryMeta[ev.category] ? categoryMeta[ev.category].cssClass : '';
      var summary = formatEventSummary(ev);
      var detail = JSON.stringify(ev.params, null, 2);

      html += '<div class="metric-event ' + cssClass + '" onclick="this.classList.toggle(\'expanded\')">' +
        '<span class="me-ts">' + ts + '</span>' +
        '<span class="me-cat">[' + (ev.logName || ev.category) + ']</span>' +
        '<span class="me-msg">' + summary + '</span>' +
        '<div class="metric-event-detail">' + detail + '</div>' +
        '</div>';
    }
    eventListEl.innerHTML = html;
  }

  function renderStatistics() {
    if (!metricPanels) return;
    var panel = metricPanels.stats;
    var elapsed = Math.floor((Date.now() - sessionStartTime) / 1000);
    var minutes = Math.floor(elapsed / 60);
    var seconds = elapsed % 60;

    var html = '<div class="metric-stat-section">Session Info</div>' +
      '<table class="metric-stat-table">' +
      '<tr><td>Session Duration</td><td>' + minutes + 'm ' + seconds.toString().padStart(2, '0') + 's</td></tr>' +
      '<tr><td>Total Events Captured</td><td>' + metricEvents.length + '</td></tr>' +
      '</table>';

    html += '<div class="metric-stat-section">Event Counts by Category</div>' +
      '<table class="metric-stat-table">';

    var total = 0;
    allCategories.forEach(function (cat) {
      var count = metricEventCounts[cat] || 0;
      total += count;
      html += '<tr><td>unity_' + cat + '</td><td>' + count + '</td></tr>';
    });
    Object.keys(metricEventCounts).forEach(function (key) {
      if (allCategories.indexOf(key) === -1) {
        var count = metricEventCounts[key];
        total += count;
        html += '<tr><td>' + key + '</td><td>' + count + '</td></tr>';
      }
    });
    html += '<tr class="total-row"><td>Total</td><td>' + total + '</td></tr>';
    html += '</table>';

    panel.innerHTML = html;
  }

  function renderPerformance() {
    if (!metricPanels) return;
    var panel = metricPanels.perf;

    var frameTime = currentFps > 0 ? (1000 / currentFps).toFixed(1) : 'N/A';

    var html = '<div class="perf-metric">' +
      '<span class="perf-label">FPS</span>' +
      '<span class="perf-value">' + currentFps + '</span></div>' +
      '<div class="perf-metric">' +
      '<span class="perf-label">Frame Time</span>' +
      '<span class="perf-value">' + frameTime + 'ms</span></div>';

    if (performance.memory) {
      var usedMB = (performance.memory.usedJSHeapSize / (1024 * 1024)).toFixed(1);
      var totalMB = (performance.memory.totalJSHeapSize / (1024 * 1024)).toFixed(1);
      var limitMB = (performance.memory.jsHeapSizeLimit / (1024 * 1024)).toFixed(0);
      html += '<div class="perf-metric">' +
        '<span class="perf-label">JS Heap (Used / Total / Limit)</span>' +
        '<span class="perf-value">' + usedMB + ' / ' + totalMB + ' / ' + limitMB + ' MB</span></div>';
    } else {
      html += '<div class="perf-metric">' +
        '<span class="perf-label">JS Heap</span>' +
        '<span class="perf-value" style="color:#666;">N/A (Chrome only)</span></div>';
    }

    var frameStalls = metricEventCounts['frame_stall'] || 0;
    var gcCount = metricEventCounts['gc'] || 0;
    html += '<div class="perf-metric">' +
      '<span class="perf-label">Frame Stalls (&gt;500ms)</span>' +
      '<span class="perf-value" style="color:' + (frameStalls > 0 ? '#fb923c' : '#00ff00') + ';">' + frameStalls + '</span></div>' +
      '<div class="perf-metric">' +
      '<span class="perf-label">GC Collections</span>' +
      '<span class="perf-value">' + gcCount + '</span></div>';

    html += '<div class="fps-graph-container">' +
      '<div class="fps-graph-title">FPS History (last ' + FPS_HISTORY_MAX + 's)</div>' +
      '<div class="fps-graph-canvas">';

    if (fpsHistory.length === 0) {
      html += '<div style="color:#666;width:100%;text-align:center;padding:20px 0;">Collecting data...</div>';
    } else {
      var maxFps = 70;
      for (var i = 0; i < fpsHistory.length; i++) {
        var fps = fpsHistory[i];
        var height = Math.min(100, (fps / maxFps) * 100);
        var barClass = 'fps-bar';
        if (fps < 20) barClass += ' critical';
        else if (fps < 40) barClass += ' warning';
        html += '<div class="' + barClass + '" style="height:' + height + '%" title="' + fps + ' FPS"></div>';
      }
    }
    html += '</div>' +
      '<div class="fps-graph-labels"><span>-' + FPS_HISTORY_MAX + 's</span><span>now</span></div>' +
      '</div>';

    panel.innerHTML = html;
  }

  function updateMetricExplorerUI() {
    if (!isMetricsVisible) return;
    if (!ensureWired()) return;
    if (currentSubtab === 'events') renderEventList();
    else if (currentSubtab === 'stats') renderStatistics();
    else if (currentSubtab === 'perf') renderPerformance();
  }

  // =========================================================================
  // 클립보드 복사 — 복사 텍스트 빌더 + SDK API 우선 폴백 체인 + 버튼 피드백
  //  복사 경로 우선순위(사용자 요구): Apps in Toss 네이티브 브릿지(setClipboardText)
  //  → navigator.clipboard.writeText → execCommand(레거시 최후수단). Toss 웹뷰에서는
  //  navigator.clipboard 가 보안컨텍스트/권한 제약으로 조용히 실패하므로 브릿지가 1순위다.
  // =========================================================================
  function pad2(n) { return n.toString().padStart(2, '0'); }

  function fmtExportTime(d) {
    return d.getFullYear() + '-' + pad2(d.getMonth() + 1) + '-' + pad2(d.getDate()) + ' ' +
           pad2(d.getHours()) + ':' + pad2(d.getMinutes()) + ':' + pad2(d.getSeconds());
  }

  function getFilteredEvents() {
    return activeFilter === 'all'
      ? metricEvents
      : metricEvents.filter(function (e) { return e.category === activeFilter; });
  }

  function buildEventsCopyText() {
    var filtered = getFilteredEvents();
    var elapsed = Math.floor((Date.now() - sessionStartTime) / 1000);
    var lines = [];
    lines.push('=== AIT Metrics — Events (filter: ' + activeFilter + ') ===');
    lines.push('Session: ' + Math.floor(elapsed / 60) + 'm ' + pad2(elapsed % 60) + 's | ' +
               filtered.length + ' events shown (' + metricEvents.length + ' total)');
    lines.push('Exported: ' + fmtExportTime(new Date()));
    lines.push('');
    // 시간 오름차순(오래된→최신): 붙여넣으면 타임라인처럼 읽히도록 화면(최신 우선)과 반대로 정렬.
    for (var i = 0; i < filtered.length; i++) {
      var ev = filtered[i];
      var d = new Date(ev.timestamp);
      var ts = pad2(d.getHours()) + ':' + pad2(d.getMinutes()) + ':' + pad2(d.getSeconds());
      lines.push('[' + ts + '] [' + (ev.logName || ev.category) + '] ' + formatEventSummary(ev));
      if (ev.params && Object.keys(ev.params).length > 0) {
        var detail = JSON.stringify(ev.params, null, 2);
        lines.push(detail.split('\n').map(function (l) { return '  ' + l; }).join('\n'));
      }
    }
    return lines.join('\n');
  }

  function buildStatsCopyText() {
    var elapsed = Math.floor((Date.now() - sessionStartTime) / 1000);
    var lines = [];
    lines.push('=== AIT Metrics — Statistics ===');
    lines.push('Exported: ' + fmtExportTime(new Date()));
    lines.push('');
    lines.push('Session Info');
    lines.push('  Session Duration: ' + Math.floor(elapsed / 60) + 'm ' + pad2(elapsed % 60) + 's');
    lines.push('  Total Events Captured: ' + metricEvents.length);
    lines.push('');
    lines.push('Event Counts by Category');
    var total = 0;
    allCategories.forEach(function (cat) {
      var c = metricEventCounts[cat] || 0; total += c;
      lines.push('  unity_' + cat + ': ' + c);
    });
    Object.keys(metricEventCounts).forEach(function (key) {
      if (allCategories.indexOf(key) === -1) {
        var c = metricEventCounts[key]; total += c;
        lines.push('  ' + key + ': ' + c);
      }
    });
    lines.push('  Total: ' + total);
    return lines.join('\n');
  }

  function buildPerfCopyText() {
    var lines = [];
    lines.push('=== AIT Metrics — Performance (snapshot) ===');
    lines.push('Exported: ' + fmtExportTime(new Date()));
    lines.push('');
    lines.push('FPS: ' + currentFps);
    lines.push('Frame Time: ' + (currentFps > 0 ? (1000 / currentFps).toFixed(1) : 'N/A') + 'ms');
    if (performance.memory) {
      lines.push('JS Heap (Used / Total / Limit): ' +
        (performance.memory.usedJSHeapSize / (1024 * 1024)).toFixed(1) + ' / ' +
        (performance.memory.totalJSHeapSize / (1024 * 1024)).toFixed(1) + ' / ' +
        (performance.memory.jsHeapSizeLimit / (1024 * 1024)).toFixed(0) + ' MB');
    } else {
      lines.push('JS Heap: N/A (Chrome only)');
    }
    lines.push('Frame Stalls (>500ms): ' + (metricEventCounts['frame_stall'] || 0));
    lines.push('GC Collections: ' + (metricEventCounts['gc'] || 0));
    lines.push('');
    lines.push('FPS History (last ' + FPS_HISTORY_MAX + 's, oldest→newest):');
    lines.push(fpsHistory.length ? fpsHistory.join(', ') : '(collecting...)');
    return lines.join('\n');
  }

  var LOG_COPY_MAX_CHARS = 300000; // 전체 페이로드 상한(과도한 클립보드 방지)
  function buildLogsCopyText() {
    var lines = [];
    lines.push('=== AIT Dev Console — Logs (' + logMirror.length + ' entries) ===');
    lines.push('Exported: ' + fmtExportTime(new Date()));
    lines.push('');
    for (var i = 0; i < logMirror.length; i++) {
      var e = logMirror[i];
      var d = new Date(e.ts);
      var ts = pad2(d.getHours()) + ':' + pad2(d.getMinutes()) + ':' + pad2(d.getSeconds()) +
               '.' + d.getMilliseconds().toString().padStart(3, '0');
      lines.push('[' + ts + '] [' + e.level + '] ' + e.text);
    }
    var out = lines.join('\n');
    if (out.length > LOG_COPY_MAX_CHARS) out = out.slice(0, LOG_COPY_MAX_CHARS) + '\n...output truncated';
    return out;
  }

  // --- 복사 폴백 체인 (각 티어는 Promise<사용경로문자열> 반환, 실패 시 reject) ---
  // 비동기 티어(브릿지·navigator.clipboard)는 영원히 settle 안 될 수 있다 — 브릿지 메시지
  // 유실, 권한 프롬프트 pending 등. 그대로 두면 버튼이 영구 비활성되고 다음 티어에 진입조차
  // 못 하므로 티어별 타임아웃으로 상한을 건다. 3초: Chromium 계열 transient activation(~5s)
  // 안쪽이라 1티어 타임아웃 후에도 후속 티어가 유효할 수 있고, 타임아웃 뒤 원 호출이 늦게
  // 성공해도 동일 텍스트 중복 쓰기라 무해. (3티어 execCommand 는 동기라 타임아웃 불필요.)
  var CLIPBOARD_TIER_TIMEOUT_MS = 3000;

  function withTimeout(promise, ms, label) {
    return new Promise(function (resolve, reject) {
      var timer = setTimeout(function () { reject(new Error(label + ' ' + ms + 'ms 내 응답 없음')); }, ms);
      promise.then(
        function (v) { clearTimeout(timer); resolve(v); },
        function (e) { clearTimeout(timer); reject(e); }
      );
    });
  }

  function tryNativeBridge(text) {
    // 클릭 시점에 window.AppsInToss 를 fresh read (early caching 금지 — setter/poll 경로에서
    // 로드 시점엔 아직 미정의일 수 있음). wrapTarget 일반화로 this 도 실제 네임스페이스에 바인딩됨.
    var api = window.AppsInToss;
    if (!api || typeof api.setClipboardText !== 'function') {
      return Promise.reject(new Error('AppsInToss.setClipboardText 미가용'));
    }
    var r;
    try { r = api.setClipboardText(text); }
    catch (e) { return Promise.reject(e); }
    // jslib와 동일하게 Promise/비Promise 양쪽 방어 (비Promise면 동기 성공으로 간주).
    if (r && typeof r.then === 'function') {
      return withTimeout(r, CLIPBOARD_TIER_TIMEOUT_MS, 'AppsInToss.setClipboardText')
        .then(function () { return 'AppsInToss.setClipboardText'; });
    }
    return Promise.resolve('AppsInToss.setClipboardText');
  }

  function tryWebClipboard(text) {
    if (navigator.clipboard && navigator.clipboard.writeText) {
      // writeText 도 권한 프롬프트/포커스 상태에 따라 무기한 pending 될 수 있어 동일하게 상한.
      return withTimeout(navigator.clipboard.writeText(text), CLIPBOARD_TIER_TIMEOUT_MS, 'navigator.clipboard.writeText')
        .then(function () { return 'navigator.clipboard'; });
    }
    return Promise.reject(new Error('navigator.clipboard 미가용'));
  }

  function tryExecCommand(text) {
    // 레거시 최후수단(deprecated). 프로그램적으로 value 주입 → 실제 키입력 없음 →
    // Unity 키보드 가드와 무관. #__vconsole 밖(body)에 붙이므로 preventDefault 오버라이드 영향 없음.
    return new Promise(function (resolve, reject) {
      var ta = null;
      try {
        ta = document.createElement('textarea');
        ta.value = text;
        ta.setAttribute('readonly', '');
        ta.style.cssText = 'position:fixed;top:-9999px;left:-9999px;opacity:0;';
        document.body.appendChild(ta);
        ta.focus();
        ta.select();
        var ok = document.execCommand('copy');
        if (ok) resolve('execCommand'); else reject(new Error('execCommand copy 반환 false'));
      } catch (e) {
        reject(e);
      } finally {
        if (ta && ta.parentNode) ta.parentNode.removeChild(ta);
      }
    });
  }

  function copyViaClipboard(text) {
    // 각 티어의 실패 사유를 모아 최종 에러에 담는다 — 중간 .catch 가 사유를 삼키면
    // 마지막 티어(execCommand)의 에러만 남아 1순위 브릿지가 왜 실패했는지 진단할 수 없다.
    var tierErrors = [];
    function msg(e) { return (e && e.message) || String(e); }
    return tryNativeBridge(text)
      .catch(function (e1) { tierErrors.push(e1); return tryWebClipboard(text); })
      .catch(function (e2) { tierErrors.push(e2); return tryExecCommand(text); })
      .catch(function (e3) {
        tierErrors.push(e3);
        var err = new Error('모든 복사 경로 실패: ' + tierErrors.map(msg).join(' | '));
        err.tierErrors = tierErrors;
        throw err;
      });
  }

  function setBtnFeedback(btn, ok, count) {
    var orig = btn.getAttribute('data-label') || btn.textContent;
    btn.textContent = ok
      ? ('✓ 복사됨' + (count != null ? ' (' + count + ')' : ''))
      : '✗ 실패';
    btn.classList.remove('copied-ok', 'copied-fail');
    btn.classList.add(ok ? 'copied-ok' : 'copied-fail');
    setTimeout(function () {
      btn.textContent = orig;
      btn.classList.remove('copied-ok', 'copied-fail');
      btn.disabled = false;
    }, ok ? 1500 : 2000);
  }

  // Copy/Logs 두 버튼이 하나의 OS 클립보드에 쓰므로, 버튼별 disabled 만으로는
  // 교차 클릭 시 나중에 settle 된 쪽이 클립보드를 덮어쓰는 경합이 남는다 → 공유 잠금.
  var copyInFlight = false;

  function handleCopy(btn, text, count) {
    if (copyInFlight || btn.disabled) return; // 재진입/교차 진입 방지
    copyInFlight = true;
    btn.disabled = true;
    copyViaClipboard(text).then(function (method) {
      copyInFlight = false;
      if (method) console.debug('[AIT] 클립보드 복사 경로: ' + method);
      setBtnFeedback(btn, true, count);
    }).catch(function (err) {
      copyInFlight = false;
      console.error('[AIT] 클립보드 복사 실패(모든 경로 소진):', err);
      setBtnFeedback(btn, false, null);
    });
  }

  // =========================================================================
  // Metrics 전용 CSS (vConsole .vc-* 와 충돌 방지 위해 #ait-metrics-root 스코프)
  // =========================================================================
  function injectMetricsCss() {
    var css =
      '#ait-metrics-root{display:flex;flex-direction:column;font-size:12px;color:#eee;}' +
      '#ait-metrics-root .metric-content{padding:12px 16px;}' +
      '#ait-metrics-root .metric-subtabs{position:sticky;top:0;z-index:1;display:flex;gap:6px;padding:8px 12px;border-bottom:1px solid #444;background:rgba(35,35,35,0.98);}' +
      '#ait-metrics-root .metric-subtab{background:transparent;border:1px solid #555;color:#aaa;padding:4px 12px;border-radius:3px;cursor:pointer;font-size:12px;font-family:inherit;transition:all 0.2s;}' +
      '#ait-metrics-root .metric-subtab:hover{border-color:#888;color:#ccc;}' +
      '#ait-metrics-root .metric-subtab.active{border-color:#00ff00;color:#00ff00;}' +
      '#ait-metrics-root .metric-tool-btn{background:#2a2a2a;border:1px solid #555;color:#ccc;padding:4px 10px;border-radius:3px;cursor:pointer;font-size:12px;font-family:inherit;transition:all 0.2s;}' +
      '#ait-metrics-root .metric-tool-btn:hover{border-color:#888;color:#fff;}' +
      '#ait-metrics-root .metric-tool-btn:disabled{opacity:0.6;cursor:default;}' +
      '#ait-metrics-root .metric-tool-btn.copied-ok{color:#00ff00;border-color:#00ff00;}' +
      '#ait-metrics-root .metric-tool-btn.copied-fail{color:#f87171;border-color:#f87171;}' +
      '#ait-metrics-root #metric-copy-btn{margin-left:auto;}' +
      '#ait-metrics-root .metric-filter{display:flex;flex-wrap:wrap;gap:4px;margin-bottom:12px;}' +
      '#ait-metrics-root .metric-filter-btn{background:#2a2a2a;border:1px solid #444;color:#aaa;padding:3px 8px;border-radius:3px;cursor:pointer;font-size:11px;font-family:inherit;transition:all 0.2s;}' +
      '#ait-metrics-root .metric-filter-btn:hover{border-color:#888;}' +
      '#ait-metrics-root .metric-filter-btn.active{background:#1a3a1a;border-color:#00ff00;color:#00ff00;}' +
      '#ait-metrics-root .metric-event{padding:6px 0;border-bottom:1px solid #2a2a2a;cursor:pointer;font-size:12px;line-height:1.5;}' +
      '#ait-metrics-root .metric-event:hover{background:rgba(255,255,255,0.03);}' +
      '#ait-metrics-root .metric-event .me-ts{color:#888;}' +
      '#ait-metrics-root .metric-event .me-cat{font-weight:bold;margin:0 8px;}' +
      '#ait-metrics-root .metric-event.scene .me-cat{color:#4ade80;}' +
      '#ait-metrics-root .metric-event.error .me-cat{color:#f87171;}' +
      '#ait-metrics-root .metric-event.gc .me-cat{color:#c084fc;}' +
      '#ait-metrics-root .metric-event.frame_stall .me-cat{color:#fb923c;}' +
      '#ait-metrics-root .metric-event.lifecycle .me-cat{color:#60a5fa;}' +
      '#ait-metrics-root .metric-event.screen .me-cat{color:#22d3ee;}' +
      '#ait-metrics-root .metric-event.timescale .me-cat{color:#fbbf24;}' +
      '#ait-metrics-root .metric-event.low_memory .me-cat{color:#ef4444;}' +
      '#ait-metrics-root .metric-event .me-msg{color:#ccc;}' +
      '#ait-metrics-root .metric-event-detail{background:#1a1a2e;padding:8px;margin-top:4px;font-size:11px;white-space:pre-wrap;color:#aaa;border-radius:3px;display:none;}' +
      '#ait-metrics-root .metric-event.expanded .metric-event-detail{display:block;}' +
      '#ait-metrics-root .metric-no-events{color:#666;text-align:center;padding:40px 0;font-size:13px;}' +
      '#ait-metrics-root .metric-stat-table{width:100%;border-collapse:collapse;margin-top:8px;}' +
      '#ait-metrics-root .metric-stat-table td{padding:6px 12px;border-bottom:1px solid #333;font-size:12px;}' +
      '#ait-metrics-root .metric-stat-table td:first-child{color:#ccc;}' +
      '#ait-metrics-root .metric-stat-table td:last-child{text-align:right;color:#00ff00;font-weight:bold;}' +
      '#ait-metrics-root .metric-stat-table tr.total-row td{border-top:2px solid #555;font-weight:bold;color:#fff;}' +
      '#ait-metrics-root .metric-stat-section{color:#888;font-size:11px;text-transform:uppercase;letter-spacing:1px;margin-top:20px;margin-bottom:8px;padding-bottom:4px;border-bottom:1px solid #444;}' +
      '#ait-metrics-root .perf-metric{display:flex;justify-content:space-between;align-items:baseline;padding:6px 0;border-bottom:1px solid #2a2a2a;}' +
      '#ait-metrics-root .perf-metric .perf-label{color:#888;font-size:12px;}' +
      '#ait-metrics-root .perf-metric .perf-value{color:#00ff00;font-size:16px;font-weight:bold;}' +
      '#ait-metrics-root .fps-graph-container{margin-top:16px;padding:8px;background:#1a1a1a;border-radius:4px;border:1px solid #333;}' +
      '#ait-metrics-root .fps-graph-title{color:#888;font-size:11px;margin-bottom:8px;text-transform:uppercase;letter-spacing:1px;}' +
      '#ait-metrics-root .fps-graph-canvas{width:100%;height:80px;display:flex;align-items:flex-end;gap:1px;}' +
      '#ait-metrics-root .fps-bar{flex:1;min-width:2px;background:#00ff00;transition:height 0.1s;border-radius:1px 1px 0 0;}' +
      '#ait-metrics-root .fps-bar.warning{background:#fbbf24;}' +
      '#ait-metrics-root .fps-bar.critical{background:#ef4444;}' +
      '#ait-metrics-root .fps-graph-labels{display:flex;justify-content:space-between;margin-top:4px;font-size:10px;color:#555;}';
    var style = document.createElement('style');
    style.textContent = css;
    document.head.appendChild(style);
  }

  // =========================================================================
  // Unity WebGL 키보드 캡처 우회
  //  Unity 프레임워크가 window/document capture phase에서 keydown/keypress의
  //  preventDefault()를 호출해 HTML input 문자 삽입을 막는다. 이를 우회해
  //  vConsole 입력창(#__vconsole 내부)에서는 타이핑이 되도록 한다.
  // =========================================================================
  function installUnityKeyboardGuard() {
    var cachedRoot = null;
    function vcRoot() {
      if (!cachedRoot) cachedRoot = document.getElementById('__vconsole');
      return cachedRoot;
    }

    var origPreventDefault = Event.prototype.preventDefault;
    Event.prototype.preventDefault = function () {
      if (this instanceof KeyboardEvent && this.target) {
        var r = vcRoot();
        if (r && r.contains(this.target)) {
          // 개행 외 키: preventDefault 무력화 → Unity 전역 캡처가 막는 타이핑을 우회.
          // Enter: 아래로 흘려보내 origPreventDefault 호출 → 개행 삽입을 막아
          //        vConsole REPL 실행 후 입력창에 잔여 '\n'이 남지 않게 한다.
          if (this.key !== 'Enter') return;
        }
      }
      return origPreventDefault.call(this);
    };

    // vConsole 자체 편집 요소(command/filter 입력창 등) 판별.
    function isVConsoleEditable(el, root) {
      if (!el || !root || !root.contains(el)) return false;
      var tag = el.tagName;
      return tag === 'INPUT' || tag === 'TEXTAREA' || el.isContentEditable === true;
    }

    ['keydown', 'keyup', 'keypress'].forEach(function (eventType) {
      document.addEventListener(eventType, function (e) {
        var r = vcRoot();
        if (r && e.target && r.contains(e.target)) {
          // 편집 입력창에서 Enter 만 vConsole 내부 핸들러로 통과시켜 REPL(입력 실행)을 살린다.
          // 일반 문자 키는 네이티브로 타이핑되므로(위 preventDefault 무력화) vConsole 핸들러가
          // 불필요 → 계속 차단해 게임 입력 누수를 최소화한다(Enter 만 Unity 로 새는 미세 리스크).
          if (e.key === 'Enter' && isVConsoleEditable(e.target, r)) return;
          e.stopPropagation();
          e.stopImmediatePropagation();
        }
      }, true); // capture 단계, Unity 리스너보다 먼저 등록되어 우선 실행
    });
  }

  function replayEarlyLogs() {
    var logs = window._aitEarlyLogs;
    if (logs && logs.length) {
      console.log('=== Bridge Initialization Logs ===');
      logs.forEach(function (l) { console.log('[Bridge] ' + l); });
      console.log('=== End Bridge Logs ===');
    }
  }

  // =========================================================================
  // 초기화
  // =========================================================================
  injectMetricsCss();

  var vConsole = new window.VConsole({
    theme: 'dark',
    defaultPlugins: ['system', 'network', 'storage'],
    // 최상위 maxLogNumber 는 deprecated — 경고 console.debug 가 우리 로그 미러에까지
    // 섞여 들어가므로 v3 정식 shape(log.maxLogNumber)로 전달한다.
    log: { maxLogNumber: 1000 }
  });
  window._aitVConsole = vConsole;

  var metricsPlugin = new window.VConsole.VConsolePlugin('ait_metrics', 'Metrics');
  metricsPlugin.on('renderTab', function (callback) { callback(METRICS_TAB_HTML); });
  metricsPlugin.on('ready', function () { ensureWired(); });
  metricsPlugin.on('show', function () { isMetricsVisible = true; updateMetricExplorerUI(); });
  metricsPlugin.on('hide', function () { isMetricsVisible = false; });
  vConsole.addPlugin(metricsPlugin);

  installUnityKeyboardGuard();
  replayEarlyLogs();

  console.log('[AIT] Dev Console(vConsole) 초기화 완료 — 스위치 버튼으로 열기/닫기, Metrics 탭 포함');
})();
