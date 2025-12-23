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

// src/hooks/useBooleanState/index.ts
var useBooleanState_exports = {};
__export(useBooleanState_exports, {
  useBooleanState: () => useBooleanState
});
module.exports = __toCommonJS(useBooleanState_exports);

// src/hooks/useBooleanState/useBooleanState.ts
var import_react = require("react");
function useBooleanState(defaultValue = false) {
  const [bool, setBool] = (0, import_react.useState)(defaultValue);
  const setTrue = (0, import_react.useCallback)(() => {
    setBool(true);
  }, []);
  const setFalse = (0, import_react.useCallback)(() => {
    setBool(false);
  }, []);
  const toggle = (0, import_react.useCallback)(() => {
    setBool((prevBool) => !prevBool);
  }, []);
  return [bool, setTrue, setFalse, toggle];
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  useBooleanState
});
