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

// src/hooks/useInputState/index.ts
var useInputState_exports = {};
__export(useInputState_exports, {
  useInputState: () => useInputState
});
module.exports = __toCommonJS(useInputState_exports);

// src/hooks/useInputState/useInputState.ts
var import_react = require("react");
function useInputState(initialValue = "", transformValue = echo) {
  const [value, setValue] = (0, import_react.useState)(initialValue);
  const handleValueChange = (0, import_react.useCallback)(
    ({ target: { value: value2 } }) => {
      setValue(transformValue(value2));
    },
    [transformValue]
  );
  return [value, handleValueChange];
}
function echo(v) {
  return v;
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  useInputState
});
