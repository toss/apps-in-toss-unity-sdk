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

// src/utils/buildContext/index.ts
var buildContext_exports = {};
__export(buildContext_exports, {
  buildContext: () => buildContext
});
module.exports = __toCommonJS(buildContext_exports);

// src/utils/buildContext/buildContext.tsx
var import_react = require("react");
var import_jsx_runtime = require("react/jsx-runtime");
function buildContext(contextName, defaultContextValues) {
  const Context = (0, import_react.createContext)(defaultContextValues ?? void 0);
  function Provider({ children, ...contextValues }) {
    const value = (0, import_react.useMemo)(
      () => Object.keys(contextValues).length > 0 ? contextValues : null,
      // eslint-disable-next-line react-hooks/exhaustive-deps
      [...Object.values(contextValues)]
    );
    return /* @__PURE__ */ (0, import_jsx_runtime.jsx)(Context.Provider, { value, children });
  }
  function useInnerContext() {
    const context = (0, import_react.useContext)(Context);
    if (context != null) {
      return context;
    }
    if (defaultContextValues != null) {
      return defaultContextValues;
    }
    throw new Error(`\`${contextName}Context\` must be used within \`${contextName}Provider\``);
  }
  return [Provider, useInnerContext];
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  buildContext
});
