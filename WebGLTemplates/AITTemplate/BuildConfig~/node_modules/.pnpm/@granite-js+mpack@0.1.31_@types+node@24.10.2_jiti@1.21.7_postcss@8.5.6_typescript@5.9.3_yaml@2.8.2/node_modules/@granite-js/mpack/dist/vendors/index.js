"use strict";
var __create = Object.create;
var __defProp = Object.defineProperty;
var __getOwnPropDesc = Object.getOwnPropertyDescriptor;
var __getOwnPropNames = Object.getOwnPropertyNames;
var __getProtoOf = Object.getPrototypeOf;
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
var __toESM = (mod, isNodeMode, target) => (target = mod != null ? __create(__getProtoOf(mod)) : {}, __copyProps(
  // If the importer is in node compatibility mode or this is not an ESM
  // file that has been converted to a CommonJS file using a Babel-
  // compatible transform (i.e. "__esModule" has not been set), then set
  // "default" to the CommonJS "module.exports" for node compatibility.
  isNodeMode || !mod || !mod.__esModule ? __defProp(target, "default", { value: mod, enumerable: true }) : target,
  mod
));
var __toCommonJS = (mod) => __copyProps(__defProp({}, "__esModule", { value: true }), mod);
var vendors_exports = {};
__export(vendors_exports, {
  getModule: () => getModule
});
module.exports = __toCommonJS(vendors_exports);
var import_src = __toESM(require("./metro/src"));
var import_TerminalReporter = __toESM(require("./metro/src/lib/TerminalReporter"));
var import_defaults = require("./metro-config/src/defaults");
var import_loadConfig = require("./metro-config/src/loadConfig");
var import_Terminal = __toESM(require("./metro-core/src/Terminal"));
var import_InspectorProxy = __toESM(require("./metro-inspector-proxy/src/InspectorProxy"));
const vendorModules = {
  metro: {
    Metro: import_src.default,
    TerminalReporter: import_TerminalReporter.default
  },
  "metro-config": {
    getDefaultValues: import_defaults.getDefaultValues,
    loadConfig: import_loadConfig.loadConfig,
    mergeConfig: import_loadConfig.mergeConfig
  },
  "metro-core": {
    Terminal: import_Terminal.default
  },
  "metro-inspector-proxy": {
    InspectorProxy: import_InspectorProxy.default
  }
};
function getModule(source) {
  return vendorModules[source];
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  getModule
});
