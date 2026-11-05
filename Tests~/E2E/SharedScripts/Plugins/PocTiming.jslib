mergeInto(LibraryManager.library, {
  // WebGPU PoC: page-level timing as "now|dlEnd|dlBytes|dlCount" (ms since navigation start).
  PocTiming_Snapshot: function () {
    var now = performance.now(), end = 0, bytes = 0, n = 0;
    try {
      performance.getEntriesByType('resource').forEach(function (e) {
        if (e.name.indexOf('.unityweb') < 0 && e.name.indexOf('.wasm') < 0 && e.name.indexOf('.data') < 0) return;
        n++; bytes += e.transferSize || e.encodedBodySize || 0; if (e.responseEnd > end) end = e.responseEnd;
      });
    } catch (x) {}
    var s = Math.round(now) + '|' + Math.round(end) + '|' + bytes + '|' + n;
    var len = lengthBytesUTF8(s) + 1, p = _malloc(len); stringToUTF8(s, p, len); return p;
  }
});
