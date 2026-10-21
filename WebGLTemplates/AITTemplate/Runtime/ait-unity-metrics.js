/*!
 * AIT Unity Metrics — Apps in Toss Unity SDK 개발용 Metrics 탭
 *
 * Apps in Toss 플랫폼의 "웹 디버그 툴즈"가 미니앱 페이지에 주입하는 vConsole
 * (window.__VCONSOLE_INSTANCE)에 "Unity Metrics" 탭(plugin id: unity_metrics)을 덧붙인다.
 *     Events / Statistics / Performance(FPS·JS Heap·GC·frame stall)
 *     → window.AppsInToss.eventLog/debugLog 인터셉트로 공급
 *
 * SDK는 자체 vConsole을 번들하지 않는다. __VCONSOLE_INSTANCE가 없으면(라이브/프로덕션,
 * 로컬 브라우저 등) 이 스크립트는 휴면 상태로 끝난다: rAF 루프, eventLog 래핑,
 * DOM/CSS 주입 모두 하지 않는다. 인스턴스가 나타난 뒤에만 수집을 시작한다.
 *
 * VConsolePlugin은 vConsole 인스턴스의 constructor 정적 프로퍼티로 접근하는데 공식 문서에
 * 없는 경로다. 기능 검사 후 try/catch로 감싸 실패해도 게임에는 영향이 없게 한다.
 *
 * 이 파일은 SDK 소유 코드다.
 */
(function () {
  'use strict';

  if (window._aitUnityMetricsInstalled) {
    return; // 중복 설치 방지
  }
  window._aitUnityMetricsInstalled = true;

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

  // 파라미터 키는 Runtime/Helpers/AIT.PerformanceLogger.cs 의 SendLog 호출과 맞춘다.
  function formatEventSummary(event) {
    var p = event.params;
    var cat = event.category;
    if (cat === 'gc' && p) {
      return 'Gen' + (p.generation || 0) + ' collection (total: ' + (p.gen0_total !== undefined ? p.gen0_total : '?') + ')';
    }
    if (cat === 'frame_stall' && p) {
      return (p.frame_duration_ms !== undefined ? p.frame_duration_ms : '?') + 'ms frame (threshold: ' + (p.threshold_ms || '?') + 'ms)';
    }
    if (cat === 'lifecycle' && p) {
      if (p.event_type === 'quitting') return 'quitting (session: ' + (p.session_duration_sec !== undefined ? p.session_duration_sec : '?') + 's)';
      return 'focus: ' + (p.has_focus !== undefined ? p.has_focus : '?');
    }
    if (cat === 'error' && p) {
      var msg = p.error_message || p.message || '';
      return msg.length > 60 ? msg.substring(0, 60) + '...' : msg;
    }
    if (cat === 'scene' && p) {
      if (p.event_type === 'scene_unloaded') return 'Unloaded: ' + (p.scene_name || '?');
      return 'Loaded: ' + (p.scene_name || '?') + ' (' + (p.load_mode || '?') + ')';
    }
    if (cat === 'screen' && p) {
      return (p.width || '?') + 'x' + (p.height || '?') + (p.orientation ? ' orientation: ' + p.orientation : '');
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
  function setupEventLogInterception() {
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
  }

  // =========================================================================
  // Performance 샘플러 (패널 열림 여부와 무관하게 항상 구동 → 히스토리 축적)
  // =========================================================================
  var fpsHistory = [];
  var FPS_HISTORY_MAX = 60;
  var lastFrameTime = 0;
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

    // 복사 버튼: 현재 서브탭(events/stats/perf)을 맥락에 맞게 복사.
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
    // 임시 textarea는 화면 밖에 붙였다가 즉시 제거한다.
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

  // 복사 버튼이 하나의 OS 클립보드에 쓰므로, 버튼별 disabled 만으로는
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
  // 활성화: 플랫폼 vConsole(window.__VCONSOLE_INSTANCE)에 탭을 붙이고 수집 시작
  //  - 인스턴스가 없으면 아무것도 하지 않는다(rAF·eventLog 래핑·DOM/CSS 주입 없음).
  //  - 호스트가 페이지 로드 시점에 주입하므로 이미 있으면 즉시, 없으면 DOMContentLoaded/load와
  //    짧은 폴링(총 5초)으로 기다린다. 끝내 없으면 포기(라이브/로컬 브라우저).
  //  - VConsolePlugin은 비공개 경로라 기능 검사 + try/catch. 탭 등록에 성공한 경우에만
  //    수집을 시작하므로 실패해도 게임·SDK 동작에는 영향이 없다.
  // =========================================================================
  var PLUGIN_ID = 'unity_metrics';
  var PLUGIN_NAME = 'Unity Metrics';
  var WAIT_POLL_MS = 250;
  var WAIT_MAX_MS = 5000;
  var attached = false;

  function tryAttach() {
    if (attached) return true;
    var instance = window.__VCONSOLE_INSTANCE;
    if (!instance || typeof instance.addPlugin !== 'function') return false;
    var PluginCtor = instance.constructor && instance.constructor.VConsolePlugin;
    if (typeof PluginCtor !== 'function') return false;

    attached = true; // 실패해도 재시도하지 않는다(중복 등록·반복 예외 방지)
    try {
      var plugin = new PluginCtor(PLUGIN_ID, PLUGIN_NAME);
      plugin.on('renderTab', function (callback) { callback(METRICS_TAB_HTML); });
      plugin.on('ready', function () { ensureWired(); });
      plugin.on('show', function () { isMetricsVisible = true; updateMetricExplorerUI(); });
      plugin.on('hide', function () { isMetricsVisible = false; });
      instance.addPlugin(plugin);
    } catch (e) {
      console.warn('[AIT] Unity Metrics 탭 등록 실패(무시하고 계속):', e);
      return true;
    }

    try {
      injectMetricsCss();
      setupEventLogInterception();
      lastFrameTime = performance.now();
      requestAnimationFrame(measureFps);
    } catch (e2) {
      console.warn('[AIT] Unity Metrics 수집 시작 실패(무시하고 계속):', e2);
    }
    return true;
  }

  function waitForInstance() {
    try {
      if (tryAttach()) return;
    } catch (e) { return; }

    var elapsed = 0;
    var timerId = null;
    function check() {
      var done = false;
      try { done = tryAttach(); } catch (e) { done = true; }
      if (done || elapsed >= WAIT_MAX_MS) {
        if (timerId !== null) clearInterval(timerId);
        timerId = null;
        document.removeEventListener('DOMContentLoaded', check);
        window.removeEventListener('load', check);
      }
    }
    document.addEventListener('DOMContentLoaded', check);
    window.addEventListener('load', check);
    timerId = setInterval(function () { elapsed += WAIT_POLL_MS; check(); }, WAIT_POLL_MS);
  }

  waitForInstance();
})();
