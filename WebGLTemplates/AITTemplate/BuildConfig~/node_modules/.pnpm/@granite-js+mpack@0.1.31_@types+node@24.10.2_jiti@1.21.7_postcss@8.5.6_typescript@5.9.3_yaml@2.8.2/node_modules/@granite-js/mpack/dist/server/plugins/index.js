"use strict";
var __defProp = Object.defineProperty;
var __getOwnPropDesc = Object.getOwnPropertyDescriptor;
var __getOwnPropNames = Object.getOwnPropertyNames;
var __hasOwnProp = Object.prototype.hasOwnProperty;
var __copyProps = (to, from, except, desc) => {
  if (from && typeof from === "object" || typeof from === "function") {
    for (let key of __getOwnPropNames(from))
      if (!__hasOwnProp.call(to, key) && key !== except)
        __defProp(to, key, { get: () => from[key], enumerable: !(desc = __getOwnPropDesc(from, key)) || desc.enumerable });
  }
  return to;
};
var __reExport = (target, mod, secondTarget) => (__copyProps(target, mod, "default"), secondTarget && __copyProps(secondTarget, mod, "default"));
var __toCommonJS = (mod) => __copyProps(__defProp({}, "__esModule", { value: true }), mod);
var plugins_exports = {};
module.exports = __toCommonJS(plugins_exports);
__reExport(plugins_exports, require("./debuggerPlugin"), module.exports);
__reExport(plugins_exports, require("./indexPagePlugin"), module.exports);
__reExport(plugins_exports, require("./serveBundlePlugin"), module.exports);
__reExport(plugins_exports, require("./statusPlugin"), module.exports);
__reExport(plugins_exports, require("./symbolicatePlugin"), module.exports);
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  ...require("./debuggerPlugin"),
  ...require("./indexPagePlugin"),
  ...require("./serveBundlePlugin"),
  ...require("./statusPlugin"),
  ...require("./symbolicatePlugin")
});
