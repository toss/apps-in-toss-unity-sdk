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

// src/hooks/useMap/index.ts
var useMap_exports = {};
__export(useMap_exports, {
  useMap: () => useMap
});
module.exports = __toCommonJS(useMap_exports);

// src/hooks/useMap/useMap.ts
var import_react2 = require("react");

// src/hooks/usePreservedReference/usePreservedReference.ts
var import_react = require("react");
function usePreservedReference(value, areValuesEqual = areDeeplyEqual) {
  const ref = (0, import_react.useRef)(value);
  return (0, import_react.useMemo)(() => {
    if (!areValuesEqual(ref.current, value)) {
      ref.current = value;
    }
    return ref.current;
  }, [areValuesEqual, value]);
}
function areDeeplyEqual(x, y) {
  return JSON.stringify(x) === JSON.stringify(y);
}

// src/hooks/useMap/useMap.ts
function useMap(initialState = /* @__PURE__ */ new Map()) {
  const [map, setMap] = (0, import_react2.useState)(() => new Map(initialState));
  const preservedInitialState = usePreservedReference(initialState);
  const set = (0, import_react2.useCallback)((key, value) => {
    setMap((prev) => {
      const nextMap = new Map(prev);
      nextMap.set(key, value);
      return nextMap;
    });
  }, []);
  const setAll = (0, import_react2.useCallback)((entries) => {
    setMap(() => new Map(entries));
  }, []);
  const remove = (0, import_react2.useCallback)((key) => {
    setMap((prev) => {
      const nextMap = new Map(prev);
      nextMap.delete(key);
      return nextMap;
    });
  }, []);
  const reset = (0, import_react2.useCallback)(() => {
    setMap(() => new Map(preservedInitialState));
  }, [preservedInitialState]);
  const actions = (0, import_react2.useMemo)(() => {
    return { set, setAll, remove, reset };
  }, [set, setAll, remove, reset]);
  return [map, actions];
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  useMap
});
