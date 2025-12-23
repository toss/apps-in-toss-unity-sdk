"use strict";
var __create = Object.create;
var __defProp = Object.defineProperty;
var __getOwnPropDesc = Object.getOwnPropertyDescriptor;
var __getOwnPropNames = Object.getOwnPropertyNames;
var __getProtoOf = Object.getPrototypeOf;
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
var __toESM = (mod, isNodeMode, target) => (target = mod != null ? __create(__getProtoOf(mod)) : {}, __copyProps(
  // If the importer is in node compatibility mode or this is not an ESM
  // file that has been converted to a CommonJS file using a Babel-
  // compatible transform (i.e. "__esModule" has not been set), then set
  // "default" to the CommonJS "module.exports" for node compatibility.
  isNodeMode || !mod || !mod.__esModule ? __defProp(target, "default", { value: mod, enumerable: true }) : target,
  mod
));
var __toCommonJS = (mod) => __copyProps(__defProp({}, "__esModule", { value: true }), mod);
var TransformPipeline_exports = {};
__export(TransformPipeline_exports, {
  TransformPipeline: () => TransformPipeline
});
module.exports = __toCommonJS(TransformPipeline_exports);
var fs = __toESM(require("fs/promises"));
var import_performance = require("../performance");
var import_md5 = require("../utils/md5");
class TransformPipeline {
  _beforeStep;
  _afterStep;
  steps = [];
  beforeStep(step) {
    this._beforeStep = step;
    return this;
  }
  afterStep(step) {
    this._afterStep = step;
    return this;
  }
  addStep(step, config) {
    this.steps.push([step, config ?? null]);
    return this;
  }
  async getStepContext(args) {
    const trace = import_performance.Performance.trace("get-step-context");
    const { mtimeMs } = await fs.stat(args.path);
    const key = (0, import_md5.md5)(args.path, mtimeMs);
    trace.stop();
    return { key, mtimeMs };
  }
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  TransformPipeline
});
