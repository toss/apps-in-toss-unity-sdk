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
var index_exports = {};
__export(index_exports, {
  BUNDLE_NAME: () => import_constants.BUNDLE_NAME,
  BuildUtils: () => import_operations.BuildUtils,
  Bundler: () => import_bundler.Bundler,
  DEV_SERVER_DEFAULT_HOST: () => import_constants.DEV_SERVER_DEFAULT_HOST,
  DEV_SERVER_DEFAULT_PORT: () => import_constants.DEV_SERVER_DEFAULT_PORT,
  DevServer: () => import_server.DevServer,
  EXPERIMENTAL__server: () => import_operations.EXPERIMENTAL__server,
  MetroBuildUtils: () => import_metro.MetroBuildUtils,
  SERVICE_BUNDLE_NAME: () => import_constants.SERVICE_BUNDLE_NAME,
  SHARED_BUNDLE_NAME: () => import_constants.SHARED_BUNDLE_NAME,
  getMetroConfig: () => import_metro.getMetroConfig,
  runServer: () => import_operations.runServer
});
module.exports = __toCommonJS(index_exports);
var import_bundler = require("./bundler");
var import_server = require("./server");
var import_operations = require("./operations");
var import_metro = require("./metro");
var import_constants = require("./constants");
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  BUNDLE_NAME,
  BuildUtils,
  Bundler,
  DEV_SERVER_DEFAULT_HOST,
  DEV_SERVER_DEFAULT_PORT,
  DevServer,
  EXPERIMENTAL__server,
  MetroBuildUtils,
  SERVICE_BUNDLE_NAME,
  SHARED_BUNDLE_NAME,
  getMetroConfig,
  runServer
});
