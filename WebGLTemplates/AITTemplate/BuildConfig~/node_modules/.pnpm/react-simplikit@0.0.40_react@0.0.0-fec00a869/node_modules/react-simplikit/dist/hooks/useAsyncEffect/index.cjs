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

// src/hooks/useAsyncEffect/index.ts
var useAsyncEffect_exports = {};
__export(useAsyncEffect_exports, {
  useAsyncEffect: () => useAsyncEffect
});
module.exports = __toCommonJS(useAsyncEffect_exports);

// src/hooks/useAsyncEffect/useAsyncEffect.ts
var import_react = require("react");
function useAsyncEffect(effect, deps) {
  (0, import_react.useEffect)(() => {
    let cleanup;
    effect().then((result) => {
      cleanup = result;
    });
    return () => {
      cleanup?.();
    };
  }, deps);
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  useAsyncEffect
});
