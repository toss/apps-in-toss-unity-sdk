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

// src/async-bridges.ts
var async_bridges_exports = {};
__export(async_bridges_exports, {
  closeView: () => closeView,
  openURL: () => openURL
});
module.exports = __toCommonJS(async_bridges_exports);

// src/native-modules/natives/GraniteModule.ts
var import_react_native = require("react-native");
var GraniteModule = import_react_native.TurboModuleRegistry.getEnforcing("GraniteModule");

// src/native-modules/natives/closeView.ts
async function closeView() {
  return GraniteModule.closeView();
}

// src/native-modules/natives/openURL.ts
var import_react_native2 = require("react-native");
function openURL(url) {
  return import_react_native2.Linking.openURL(url);
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  closeView,
  openURL
});
