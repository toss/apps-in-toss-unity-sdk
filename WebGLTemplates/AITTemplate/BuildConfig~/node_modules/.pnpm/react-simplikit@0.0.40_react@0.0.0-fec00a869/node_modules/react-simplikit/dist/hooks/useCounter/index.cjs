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

// src/hooks/useCounter/index.ts
var useCounter_exports = {};
__export(useCounter_exports, {
  useCounter: () => useCounter
});
module.exports = __toCommonJS(useCounter_exports);

// src/hooks/useCounter/useCounter.ts
var import_react = require("react");
function useCounter(initialValue = 0, { min, max, step = 1 } = {}) {
  const validateValue = (value) => {
    let validatedValue = value;
    if (min !== void 0 && validatedValue < min) {
      validatedValue = min;
    }
    if (max !== void 0 && validatedValue > max) {
      validatedValue = max;
    }
    return validatedValue;
  };
  const [count, setCountState] = (0, import_react.useState)(() => validateValue(initialValue));
  const validateValueMemoized = (0, import_react.useCallback)(validateValue, [min, max]);
  const setCount = (0, import_react.useCallback)(
    (value) => {
      setCountState((prev) => {
        const nextValue = typeof value === "function" ? value(prev) : value;
        return validateValueMemoized(nextValue);
      });
    },
    [validateValueMemoized]
  );
  const increment = (0, import_react.useCallback)(() => {
    setCount((prev) => prev + step);
  }, [setCount, step]);
  const decrement = (0, import_react.useCallback)(() => {
    setCount((prev) => prev - step);
  }, [setCount, step]);
  const reset = (0, import_react.useCallback)(() => {
    setCount(initialValue);
  }, [setCount, initialValue]);
  return {
    count,
    increment,
    decrement,
    reset,
    setCount
  };
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  useCounter
});
