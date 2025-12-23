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

// src/hooks/useTimeout/index.ts
var useTimeout_exports = {};
__export(useTimeout_exports, {
  useTimeout: () => useTimeout
});
module.exports = __toCommonJS(useTimeout_exports);

// src/hooks/useTimeout/useTimeout.ts
var import_react2 = require("react");

// src/hooks/usePreservedCallback/usePreservedCallback.ts
var import_react = require("react");
function usePreservedCallback(callback) {
  const callbackRef = (0, import_react.useRef)(callback);
  (0, import_react.useEffect)(() => {
    callbackRef.current = callback;
  }, [callback]);
  return (0, import_react.useCallback)((...args) => {
    return callbackRef.current(...args);
  }, []);
}

// src/hooks/useTimeout/useTimeout.ts
function useTimeout(callback, delay = 0) {
  const preservedCallback = usePreservedCallback(callback);
  (0, import_react2.useEffect)(() => {
    const timeoutId = window.setTimeout(preservedCallback, delay);
    return () => window.clearTimeout(timeoutId);
  }, [delay, preservedCallback]);
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  useTimeout
});
