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

// src/hooks/useControlledState/index.ts
var useControlledState_exports = {};
__export(useControlledState_exports, {
  useControlledState: () => useControlledState
});
module.exports = __toCommonJS(useControlledState_exports);

// src/hooks/useControlledState/useControlledState.ts
var import_react = require("react");
function useControlledState({
  value: valueProp,
  defaultValue,
  onChange,
  equalityFn = Object.is
}) {
  const [uncontrolledState, setUncontrolledState] = (0, import_react.useState)(defaultValue);
  const controlled = valueProp !== void 0;
  const value = controlled ? valueProp : uncontrolledState;
  const setValue = (0, import_react.useCallback)(
    (next) => {
      const nextValue = isSetStateAction(next) ? next(value) : next;
      if (equalityFn(value, nextValue) === true) return;
      if (controlled === false) setUncontrolledState(nextValue);
      if (controlled === true && nextValue === void 0) setUncontrolledState(nextValue);
      onChange?.(nextValue);
    },
    [controlled, onChange, equalityFn, value]
  );
  return [value, setValue];
}
function isSetStateAction(next) {
  return typeof next === "function";
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  useControlledState
});
