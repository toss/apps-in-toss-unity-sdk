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
var getId_exports = {};
__export(getId_exports, {
  getId: () => getId
});
module.exports = __toCommonJS(getId_exports);
var import_md5 = require("./md5");
var import_constants = require("../constants");
function getId(bundleConfig) {
  return (0, import_md5.md5)(
    JSON.stringify([
      import_constants.VERSION,
      bundleConfig.rootDir,
      bundleConfig.dev,
      bundleConfig.buildConfig.resolver,
      bundleConfig.buildConfig.transformer,
      bundleConfig.buildConfig.platform,
      bundleConfig.buildConfig.babel,
      bundleConfig.buildConfig.swc,
      bundleConfig.buildConfig.esbuild?.loader,
      bundleConfig.buildConfig.esbuild?.resolveExtensions,
      bundleConfig.buildConfig.esbuild?.mainFields,
      bundleConfig.buildConfig.esbuild?.conditions
    ])
  );
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  getId
});
