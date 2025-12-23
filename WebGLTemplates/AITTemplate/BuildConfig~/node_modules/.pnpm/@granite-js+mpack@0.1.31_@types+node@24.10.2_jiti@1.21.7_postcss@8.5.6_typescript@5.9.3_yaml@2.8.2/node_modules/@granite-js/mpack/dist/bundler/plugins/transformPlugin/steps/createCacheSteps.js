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
var createCacheSteps_exports = {};
__export(createCacheSteps_exports, {
  createCacheSteps: () => createCacheSteps
});
module.exports = __toCommonJS(createCacheSteps_exports);
var import_cache = require("../../../../cache");
var import_performance = require("../../../../performance");
function createCacheSteps(config) {
  const transformCache = new import_cache.Cache(`transformed-${config.id}`, {
    parse: (data) => ({ transformed: data, mtimeMs: 0 }),
    stringify: (value) => value.transformed
  });
  const readCodeFromCache = async (code, _args, context) => {
    if (!config.enabled) {
      return { code };
    }
    const cache = await import_performance.Performance.withTrace(() => transformCache.read(context.key), {
      name: "read-cache"
    });
    return cache?.transformed ? { code: cache.transformed, done: true } : { code };
  };
  const writeCodeToCache = async (code, _args, context) => {
    if (config.enabled) {
      const trace = import_performance.Performance.trace("write-cache");
      await transformCache.write(context.key, {
        mtimeMs: context.mtimeMs,
        transformed: code
      });
      trace.stop();
    }
    return { code };
  };
  return { beforeTransform: readCodeFromCache, afterTransform: writeCodeToCache };
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  createCacheSteps
});
