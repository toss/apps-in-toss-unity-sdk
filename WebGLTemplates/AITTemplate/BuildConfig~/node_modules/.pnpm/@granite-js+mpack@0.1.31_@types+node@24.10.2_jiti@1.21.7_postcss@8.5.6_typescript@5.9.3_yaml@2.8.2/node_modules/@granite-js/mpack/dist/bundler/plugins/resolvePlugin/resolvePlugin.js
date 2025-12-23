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
var resolvePlugin_exports = {};
__export(resolvePlugin_exports, {
  resolvePlugin: () => resolvePlugin
});
module.exports = __toCommonJS(resolvePlugin_exports);
var import_setupAliasResolver = require("./alias/setupAliasResolver");
var import_setupProtocolResolver = require("./protocol/setupProtocolResolver");
function resolvePlugin({ context }) {
  return {
    name: "resolve-plugin",
    setup(build) {
      const { buildConfig } = context.config;
      const { resolver } = buildConfig;
      const alias = resolver?.alias ?? [];
      const protocols = resolver?.protocols ?? {};
      (0, import_setupAliasResolver.setupAliasResolver)(build, alias);
      (0, import_setupProtocolResolver.setupProtocolResolver)(build, protocols);
    }
  };
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  resolvePlugin
});
