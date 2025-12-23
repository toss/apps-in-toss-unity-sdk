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

// src/hooks/useInterval/index.ts
var useInterval_exports = {};
__export(useInterval_exports, {
  useInterval: () => useInterval
});
module.exports = __toCommonJS(useInterval_exports);

// src/hooks/useInterval/useInterval.ts
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

// src/hooks/useInterval/useInterval.ts
function useInterval(callback, options) {
  const delay = typeof options === "number" ? options : options.delay;
  const immediate = typeof options === "number" ? false : options.immediate;
  const enabled = typeof options === "number" ? true : options.enabled ?? true;
  const preservedCallback = usePreservedCallback(callback);
  (0, import_react2.useEffect)(() => {
    if (immediate === true && enabled) {
      preservedCallback();
    }
  }, [immediate, preservedCallback, enabled]);
  (0, import_react2.useEffect)(() => {
    if (!enabled) {
      return;
    }
    const id = window.setInterval(preservedCallback, delay);
    return () => window.clearInterval(id);
  }, [delay, preservedCallback, enabled]);
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  useInterval
});
