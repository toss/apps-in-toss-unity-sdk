mergeInto(LibraryManager.library, {
  // WebGPU PoC benchmark: time base (ms since navigation start).
  PocBench_Now: function () {
    return performance.now();
  },
  // Publishes the benchmark report to window.__pocBench and upserts it into localStorage 'pocBenchRuns' (max 30).
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
    try {
      var runs = [];
      try { runs = JSON.parse(localStorage.getItem('pocBenchRuns') || '[]'); } catch (e) { runs = []; }
      if (!Array.isArray(runs)) runs = [];
      var idx = -1;
      for (var i = 0; i < runs.length; i++) { if (runs[i] && runs[i].runId === j.runId) { idx = i; break; } }
      if (idx >= 0) runs[idx] = j; else runs.push(j);
      while (runs.length > 30) runs.shift();
      localStorage.setItem('pocBenchRuns', JSON.stringify(runs));
    } catch (e) {}
  }
});
