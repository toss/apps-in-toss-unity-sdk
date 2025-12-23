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
var AsyncTransformPipeline_exports = {};
__export(AsyncTransformPipeline_exports, {
  AsyncTransformPipeline: () => AsyncTransformPipeline
});
module.exports = __toCommonJS(AsyncTransformPipeline_exports);
var import_TransformPipeline = require("./TransformPipeline");
var import_performance = require("../performance");
class AsyncTransformPipeline extends import_TransformPipeline.TransformPipeline {
  async transform(code, args) {
    const context = await this.getStepContext(args);
    const before = (code2, args2, context2) => {
      return this._beforeStep ? this._beforeStep(code2, args2, context2) : Promise.resolve({ code: code2, done: false });
    };
    const after = (code2, args2, context2) => {
      return this._afterStep ? this._afterStep(code2, args2, context2) : Promise.resolve({ code: code2, done: true });
    };
    let result = await before(code, args, context);
    for await (const [step, config] of this.steps) {
      if (result.done) {
        break;
      }
      if (config?.conditions == null || Array.isArray(config?.conditions) && config.conditions.some((condition) => condition(result.code, args.path))) {
        let trace;
        if (typeof step.name === "string") {
          trace = import_performance.Performance.trace(`step-${step.name}`, { detail: { file: args.path } });
        }
        result = await step(result.code, args, context);
        trace?.stop();
        if (config?.skipOtherSteps) {
          break;
        }
      }
    }
    return after(result.code, args, context);
  }
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  AsyncTransformPipeline
});
