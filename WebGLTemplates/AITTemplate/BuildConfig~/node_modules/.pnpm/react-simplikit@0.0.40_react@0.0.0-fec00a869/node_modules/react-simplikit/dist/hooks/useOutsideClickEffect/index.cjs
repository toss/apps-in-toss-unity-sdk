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

// src/hooks/useOutsideClickEffect/index.ts
var useOutsideClickEffect_exports = {};
__export(useOutsideClickEffect_exports, {
  useOutsideClickEffect: () => useOutsideClickEffect
});
module.exports = __toCommonJS(useOutsideClickEffect_exports);

// src/hooks/useOutsideClickEffect/useOutsideClickEffect.ts
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

// src/hooks/useOutsideClickEffect/useOutsideClickEffect.ts
function useOutsideClickEffect(container, callback) {
  const containers = (0, import_react2.useRef)([]);
  const handleDocumentClick = usePreservedCallback(({ target }) => {
    if (target === null) {
      return;
    }
    if (containers.current.length === 0) {
      return;
    }
    if (containers.current.some((x) => x.contains(target))) {
      return;
    }
    callback();
  });
  (0, import_react2.useEffect)(() => {
    containers.current = [container].flat(1).filter((item) => item != null);
  }, [container]);
  (0, import_react2.useEffect)(() => {
    document.addEventListener("click", handleDocumentClick);
    return () => {
      document.removeEventListener("click", handleDocumentClick);
    };
  }, [handleDocumentClick]);
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  useOutsideClickEffect
});
