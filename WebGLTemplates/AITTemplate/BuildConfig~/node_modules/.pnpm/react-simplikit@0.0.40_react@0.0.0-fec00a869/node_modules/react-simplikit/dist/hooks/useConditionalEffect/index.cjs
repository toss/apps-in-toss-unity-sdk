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

// src/hooks/useConditionalEffect/index.ts
var useConditionalEffect_exports = {};
__export(useConditionalEffect_exports, {
  useConditionalEffect: () => useConditionalEffect
});
module.exports = __toCommonJS(useConditionalEffect_exports);

// src/hooks/useConditionalEffect/useConditionalEffect.ts
var import_react = require("react");
function useConditionalEffect(effect, deps, condition) {
  const prevDepsRef = (0, import_react.useRef)(void 0);
  const memoizedCondition = (0, import_react.useCallback)(condition, deps);
  if (deps.length === 0) {
    console.warn(
      "useConditionalEffect received an empty dependency array. This may indicate missing dependencies and could lead to unexpected behavior."
    );
  }
  const shouldRun = memoizedCondition(prevDepsRef.current, deps);
  (0, import_react.useEffect)(() => {
    if (shouldRun) {
      const cleanup = effect();
      prevDepsRef.current = deps;
      return cleanup;
    }
    prevDepsRef.current = deps;
  }, deps);
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  useConditionalEffect
});
