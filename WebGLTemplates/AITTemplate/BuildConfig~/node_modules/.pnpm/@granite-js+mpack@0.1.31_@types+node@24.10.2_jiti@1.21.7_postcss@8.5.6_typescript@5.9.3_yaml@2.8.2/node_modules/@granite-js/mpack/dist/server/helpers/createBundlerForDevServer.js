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
var createBundlerForDevServer_exports = {};
__export(createBundlerForDevServer_exports, {
  createBundlerForDevServer: () => createBundlerForDevServer
});
module.exports = __toCommonJS(createBundlerForDevServer_exports);
var import_bundler = require("../../bundler");
var import_getBundleName = require("../../utils/getBundleName");
var import_constants = require("../constants");
async function createBundlerForDevServer({
  rootDir,
  platform,
  buildConfig
}) {
  const bundleName = (0, import_getBundleName.getBundleName)(import_constants.DEV_SERVER_BUNDLE_NAME);
  return new import_bundler.Bundler({
    rootDir,
    dev: true,
    cache: true,
    metafile: false,
    buildConfig: {
      ...buildConfig,
      platform,
      outfile: bundleName
    }
  });
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  createBundlerForDevServer
});
