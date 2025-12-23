"use strict";
var __defProp = Object.defineProperty;
var __getOwnPropDesc = Object.getOwnPropertyDescriptor;
var __getOwnPropNames = Object.getOwnPropertyNames;
var __hasOwnProp = Object.prototype.hasOwnProperty;
var __export = (target, all) => {
  for (var name in all)
    __defProp(target, name, { get: all[name], enumerable: true });
};
var __copyProps = (to, from, except, desc) => {
  if (from && typeof from === "object" || typeof from === "function") {
    for (let key of __getOwnPropNames(from))
      if (!__hasOwnProp.call(to, key) && key !== except)
        __defProp(to, key, { get: () => from[key], enumerable: !(desc = __getOwnPropDesc(from, key)) || desc.enumerable });
  }
  return to;
};
var __toCommonJS = (mod) => __copyProps(__defProp({}, "__esModule", { value: true }), mod);
var Performance_exports = {};
__export(Performance_exports, {
  Performance: () => Performance
});
module.exports = __toCommonJS(Performance_exports);
var import_perf_hooks = require("perf_hooks");
var import_es_toolkit = require("es-toolkit");
var TracePhase = /* @__PURE__ */ ((TracePhase2) => {
  TracePhase2["Start"] = "start";
  TracePhase2["End"] = "end";
  TracePhase2["Measure"] = "measure";
  return TracePhase2;
})(TracePhase || {});
const sequences = {};
const records = {};
function getName(name, phase, sequence) {
  return `${name}.${phase}#${sequence}`;
}
function parseName(name) {
  const [traceName] = name.split(".");
  return traceName ?? "";
}
const PerformanceImpl = {
  trace(name, options) {
    const sequence = typeof sequences[name] === "number" ? ++sequences[name] : sequences[name] = 0;
    const startName = getName(name, "start" /* Start */, sequence);
    const endName = getName(name, "end" /* End */, sequence);
    const measureName = getName(name, "measure" /* Measure */, sequence);
    const startMark = import_perf_hooks.performance.mark(startName, options);
    return {
      stop: (options2) => {
        const endMark = import_perf_hooks.performance.mark(endName, options2);
        const measureResult = import_perf_hooks.performance.measure(measureName, startName, endName);
        records[measureName] = {
          sequence,
          duration: measureResult.duration,
          name: measureResult.name,
          startTime: measureResult.startTime,
          detail: {
            ...startMark.detail ?? {},
            ...endMark.detail ?? {}
          }
        };
        import_perf_hooks.performance.clearMarks(startName);
        import_perf_hooks.performance.clearMarks(endName);
        import_perf_hooks.performance.clearMeasures(measureName);
      }
    };
  },
  async withTrace(task, config) {
    const trace = PerformanceImpl.trace(config.name, config.startOptions);
    try {
      const result = await task();
      const stopMarkOptions = config.stopOptions?.(result);
      trace.stop(stopMarkOptions);
      return result;
    } catch (error) {
      trace.stop({ detail: { error } });
      throw error;
    }
  },
  getSummary() {
    const results = {};
    Object.entries(records).map(([measureName, performanceMeasure]) => {
      const name = parseName(measureName);
      const result = {
        sequence: performanceMeasure.sequence,
        startTime: performanceMeasure.startTime,
        duration: performanceMeasure.duration,
        ...Object.keys(performanceMeasure.detail).length > 0 ? { detail: performanceMeasure.detail } : null
      };
      if (results[name] === void 0) {
        results[name] = { averageDuration: 0, records: [result] };
      } else {
        results[name].records.push(result);
      }
    });
    Object.values(results).forEach((result) => {
      result.averageDuration = result.records.reduce((acc, curr) => acc + curr.duration, 0) / result.records.length;
    });
    return results;
  }
};
const PerformanceShim = {
  // eslint-disable-next-line @typescript-eslint/no-unused-vars
  trace(_name) {
    return { stop: import_es_toolkit.noop };
  },
  // eslint-disable-next-line @typescript-eslint/no-unused-vars
  async withTrace(task, _config) {
    const result = await task();
    return result;
  },
  getSummary() {
    return null;
  }
};
const perfTraceEnabled = process.env.MPACK_TRACE === "1";
const Performance = perfTraceEnabled ? PerformanceImpl : PerformanceShim;
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  Performance
});
