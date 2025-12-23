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

// src/hooks/useDoubleClick/index.ts
var useDoubleClick_exports = {};
__export(useDoubleClick_exports, {
  useDoubleClick: () => useDoubleClick
});
module.exports = __toCommonJS(useDoubleClick_exports);

// src/hooks/useDoubleClick/useDoubleClick.ts
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

// src/hooks/useDoubleClick/useDoubleClick.ts
function useDoubleClick({
  delay = 250,
  click,
  doubleClick
}) {
  const clickTimeout = (0, import_react2.useRef)(null);
  const clearClickTimeout = usePreservedCallback(() => {
    if (clickTimeout.current != null) {
      window.clearTimeout(clickTimeout.current);
      clickTimeout.current = null;
    }
  });
  (0, import_react2.useEffect)(() => () => clearClickTimeout(), [clearClickTimeout]);
  const handleEvent = (0, import_react2.useCallback)(
    (event) => {
      clearClickTimeout();
      if (click && event.detail === 1) {
        clickTimeout.current = window.setTimeout(() => {
          click(event);
        }, delay);
      }
      if (event.detail === 2) {
        doubleClick(event);
      }
    },
    [click, doubleClick, delay, clearClickTimeout]
  );
  return handleEvent;
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  useDoubleClick
});
