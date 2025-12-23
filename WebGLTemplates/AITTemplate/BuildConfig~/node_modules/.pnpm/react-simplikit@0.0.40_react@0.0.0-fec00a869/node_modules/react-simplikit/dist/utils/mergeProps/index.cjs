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

// src/utils/mergeProps/index.ts
var mergeProps_exports = {};
__export(mergeProps_exports, {
  mergeProps: () => mergeProps
});
module.exports = __toCommonJS(mergeProps_exports);

// src/utils/mergeProps/mergeProps.ts
function mergeProps(...props) {
  return props.reduce(pushProp, {});
}
function pushProp(prev, curr) {
  for (const key in curr) {
    if (curr[key] === void 0) continue;
    switch (key) {
      case "className": {
        prev[key] = [prev[key], curr[key]].join(" ").trim();
        break;
      }
      case "style": {
        prev[key] = mergeStyle(prev[key], curr[key]);
        break;
      }
      default: {
        const mergedFunction = mergeFunction(prev[key], curr[key]);
        if (mergedFunction) {
          prev[key] = mergedFunction;
        } else if (curr[key] !== void 0) {
          prev[key] = curr[key];
        }
      }
    }
  }
  return prev;
}
function mergeStyle(a, b) {
  if (a == null) return b;
  return { ...a, ...b };
}
function mergeFunction(a, b) {
  if (typeof a === "function" && typeof b === "function") {
    return (...args) => {
      a(...args);
      b(...args);
    };
  }
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  mergeProps
});
