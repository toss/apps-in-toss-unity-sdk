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

// src/components/Separated/index.ts
var Separated_exports = {};
__export(Separated_exports, {
  Separated: () => Separated
});
module.exports = __toCommonJS(Separated_exports);

// src/components/Separated/Separated.tsx
var import_react = require("react");
var import_jsx_runtime = require("react/jsx-runtime");
function Separated({ children, by: separator }) {
  const childrenArray = import_react.Children.toArray(children).filter(import_react.isValidElement);
  return /* @__PURE__ */ (0, import_jsx_runtime.jsx)(import_jsx_runtime.Fragment, { children: childrenArray.map((child, i, { length }) => /* @__PURE__ */ (0, import_jsx_runtime.jsxs)(import_react.Fragment, { children: [
    child,
    i + 1 !== length && separator
  ] }, i)) });
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  Separated
});
