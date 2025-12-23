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
var resolveHelpers_exports = {};
__export(resolveHelpers_exports, {
  createNonRecursiveResolver: () => createNonRecursiveResolver,
  isResolved: () => isResolved
});
module.exports = __toCommonJS(resolveHelpers_exports);
const RESOLVED_FLAG_KEY = Symbol.for("mpack:REAOLVED_FLAG");
function createNonRecursiveResolver(build) {
  return (args, options) => {
    if (args.pluginData?.[RESOLVED_FLAG_KEY]) {
      return null;
    }
    const pluginDataWithResolvedFlag = {
      ...options?.pluginData,
      [RESOLVED_FLAG_KEY]: true
    };
    return build.resolve(args.path, { ...options, pluginData: pluginDataWithResolvedFlag });
  };
}
function isResolved(args) {
  return args.pluginData?.[RESOLVED_FLAG_KEY];
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  createNonRecursiveResolver,
  isResolved
});
