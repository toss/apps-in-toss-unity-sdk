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

// src/hooks/useVisibilityEvent/index.ts
var useVisibilityEvent_exports = {};
__export(useVisibilityEvent_exports, {
  useVisibilityEvent: () => useVisibilityEvent
});
module.exports = __toCommonJS(useVisibilityEvent_exports);

// src/hooks/useVisibilityEvent/useVisibilityEvent.ts
var import_react = require("react");
function useVisibilityEvent(callback, options = {}) {
  const handleVisibilityChange = (0, import_react.useCallback)(() => {
    callback(document.visibilityState);
  }, [callback]);
  (0, import_react.useEffect)(() => {
    if (options?.immediate ?? false) {
      handleVisibilityChange();
    }
    document.addEventListener("visibilitychange", handleVisibilityChange);
    return () => {
      document.removeEventListener("visibilitychange", handleVisibilityChange);
    };
  }, [handleVisibilityChange, options?.immediate]);
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  useVisibilityEvent
});
