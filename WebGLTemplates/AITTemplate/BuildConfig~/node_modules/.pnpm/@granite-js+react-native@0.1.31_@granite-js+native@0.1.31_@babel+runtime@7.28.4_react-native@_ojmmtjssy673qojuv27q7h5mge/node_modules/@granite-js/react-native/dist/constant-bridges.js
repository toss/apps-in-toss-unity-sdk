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

// src/constant-bridges.ts
var constant_bridges_exports = {};
__export(constant_bridges_exports, {
  getSchemeUri: () => getSchemeUri
});
module.exports = __toCommonJS(constant_bridges_exports);

// src/native-modules/natives/GraniteModule.ts
var import_react_native = require("react-native");
var GraniteModule = import_react_native.TurboModuleRegistry.getEnforcing("GraniteModule");

// src/native-modules/natives/getSchemeUri.ts
function getSchemeUri() {
  return GraniteModule.schemeUri;
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  getSchemeUri
});
