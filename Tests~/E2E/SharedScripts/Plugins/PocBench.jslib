mergeInto(LibraryManager.library, {
  // WebGPU PoC benchmark: time base (ms since navigation start).
  PocBench_Now: function () {
    return performance.now();
  },
  // Runtime config injected by the page (window.__pocCfg). Returns a JSON string ("null" when absent).
  PocBench_GetCfg: function () {
    var s = 'null';
    try { s = JSON.stringify(window.__pocCfg || null); } catch (e) { s = 'null'; }
    var n = lengthBytesUTF8(s) + 1;
    var p = _malloc(n);
    stringToUTF8(s, p, n);
    return p;
  },
  // WebAssembly heap size in MB.
  PocBench_WasmHeapMB: function () {
    try { return HEAPU8.length / 1048576; } catch (e) { return -1; }
  },
  // Pushes a JPEG thumbnail (base64) onto window.__pocThumbs; the page side drains the array.
  PocBench_Thumb: function (idPtr, b64Ptr) {
    try {
      if (!window.__pocThumbs) window.__pocThumbs = [];
      window.__pocThumbs.push({ id: UTF8ToString(idPtr), t: performance.now(), b64: UTF8ToString(b64Ptr) });
    } catch (e) {}
  },
  // Publishes the benchmark report to window.__pocBench and upserts it into localStorage 'pocBenchRuns' (max 30).
  // The localStorage copy omits the per-frame arrays (dts/cpus); the full report stays in window.__pocBench
  // and the latest full copy is kept in localStorage 'pocBenchLast'.
  PocBench_Publish: function (jsonPtr) {
    var j;
    try { j = JSON.parse(UTF8ToString(jsonPtr)); } catch (e) { return; }
    try {
      j.dpr = window.devicePixelRatio;
      j.innerW = window.innerWidth;
      j.innerH = window.innerHeight;
      j.userAgent = navigator.userAgent;
      j.pageNowMs = performance.now();
      j.hasNavigatorGpu = !!navigator.gpu;
    } catch (e) {}
    window.__pocBench = j;
    try { localStorage.setItem('pocBenchLast', JSON.stringify(j)); } catch (e) {}
    try {
      var slim = JSON.parse(JSON.stringify(j));
      if (slim.stages) for (var s = 0; s < slim.stages.length; s++) { delete slim.stages[s].dts; delete slim.stages[s].cpus; }
      var runs = [];
      try { runs = JSON.parse(localStorage.getItem('pocBenchRuns') || '[]'); } catch (e) { runs = []; }
      if (!Array.isArray(runs)) runs = [];
      var idx = -1;
      for (var i = 0; i < runs.length; i++) { if (runs[i] && runs[i].runId === slim.runId) { idx = i; break; } }
      if (idx >= 0) runs[idx] = slim; else runs.push(slim);
      while (runs.length > 30) runs.shift();
      localStorage.setItem('pocBenchRuns', JSON.stringify(runs));
    } catch (e) {}
  }
});
