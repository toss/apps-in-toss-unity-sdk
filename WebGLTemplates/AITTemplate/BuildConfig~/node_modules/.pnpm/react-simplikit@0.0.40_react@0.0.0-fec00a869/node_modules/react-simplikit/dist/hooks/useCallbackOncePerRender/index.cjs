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

// src/hooks/useCallbackOncePerRender/index.ts
var useCallbackOncePerRender_exports = {};
__export(useCallbackOncePerRender_exports, {
  useCallbackOncePerRender: () => useCallbackOncePerRender
});
module.exports = __toCommonJS(useCallbackOncePerRender_exports);

// src/hooks/useCallbackOncePerRender/useCallbackOncePerRender.ts
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

// src/hooks/useCallbackOncePerRender/useCallbackOncePerRender.ts
function useCallbackOncePerRender(callback, deps) {
  const hasFired = (0, import_react2.useRef)(false);
  (0, import_react2.useEffect)(() => {
    hasFired.current = false;
  }, deps);
  return usePreservedCallback((...args) => {
    if (hasFired.current) {
      return;
    }
    callback(...args);
    hasFired.current = true;
  });
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  useCallbackOncePerRender
});
