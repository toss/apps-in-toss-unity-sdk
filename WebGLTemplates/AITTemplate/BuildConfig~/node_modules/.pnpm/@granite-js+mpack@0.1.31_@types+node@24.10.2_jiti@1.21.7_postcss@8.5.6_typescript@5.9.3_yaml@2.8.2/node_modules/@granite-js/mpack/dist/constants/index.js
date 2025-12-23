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
var constants_exports = {};
__export(constants_exports, {
  ASSET_EXTENSIONS: () => ASSET_EXTENSIONS,
  BUNDLE_NAME: () => BUNDLE_NAME,
  DEBUGGER_FRONTEND_PATH: () => DEBUGGER_FRONTEND_PATH,
  DEV_SERVER_DEFAULT_HOST: () => DEV_SERVER_DEFAULT_HOST,
  DEV_SERVER_DEFAULT_PORT: () => DEV_SERVER_DEFAULT_PORT,
  INTERNAL_LOAD_REMOTE_IDENTIFIER: () => INTERNAL_LOAD_REMOTE_IDENTIFIER,
  INTERNAL_NAMESPACE_IDENTIFIER: () => INTERNAL_NAMESPACE_IDENTIFIER,
  MPACK_CACHE_DIR: () => MPACK_CACHE_DIR,
  MPACK_DATA_DIR: () => MPACK_DATA_DIR,
  PRELUDE_PROTOCOL: () => PRELUDE_PROTOCOL,
  REQUIRE_CONTEXT_PROTOCOL: () => REQUIRE_CONTEXT_PROTOCOL,
  RESOLVER_EXPORTS_MAP_CONDITIONS: () => RESOLVER_EXPORTS_MAP_CONDITIONS,
  RESOLVER_MAIN_FIELDS: () => RESOLVER_MAIN_FIELDS,
  SERVICE_BUNDLE_NAME: () => SERVICE_BUNDLE_NAME,
  SHARED_BUNDLE_NAME: () => SHARED_BUNDLE_NAME,
  SOURCE_EXTENSIONS: () => SOURCE_EXTENSIONS,
  VERSION: () => VERSION
});
module.exports = __toCommonJS(constants_exports);
var fs = __toESM(require("fs"));
var os = __toESM(require("os"));
var path = __toESM(require("path"));
var import_getBundleName = require("../utils/getBundleName");
const TEMP_DIR = path.join(os.tmpdir(), "mpack");
const VERSION = JSON.parse(fs.readFileSync(path.join(__dirname, "../../package.json"), "utf-8")).version;
const REQUIRE_CONTEXT_PROTOCOL = "require-context:";
const PRELUDE_PROTOCOL = "prelude:";
const MPACK_DATA_DIR = path.join(TEMP_DIR, "data");
const MPACK_CACHE_DIR = path.join(TEMP_DIR, "cache");
const SOURCE_EXTENSIONS = [".tsx", ".ts", ".jsx", ".js", ".json"];
const ASSET_EXTENSIONS = [".png", ".jpg", ".jpeg", ".gif", ".webp", ".svg"];
const DEV_SERVER_DEFAULT_HOST = "0.0.0.0";
const DEV_SERVER_DEFAULT_PORT = 8081;
const SHARED_BUNDLE_NAME = (0, import_getBundleName.getBundleName)("index");
const SERVICE_BUNDLE_NAME = (0, import_getBundleName.getBundleName)("service");
const BUNDLE_NAME = (0, import_getBundleName.getBundleName)("index");
const DEBUGGER_FRONTEND_PATH = "/debugger-frontend";
const RESOLVER_MAIN_FIELDS = ["react-native", "browser", "main"];
const RESOLVER_EXPORTS_MAP_CONDITIONS = ["react-native"];
const INTERNAL_NAMESPACE_IDENTIFIER = "__mpackInternal";
const INTERNAL_LOAD_REMOTE_IDENTIFIER = "loadRemote";
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  ASSET_EXTENSIONS,
  BUNDLE_NAME,
  DEBUGGER_FRONTEND_PATH,
  DEV_SERVER_DEFAULT_HOST,
  DEV_SERVER_DEFAULT_PORT,
  INTERNAL_LOAD_REMOTE_IDENTIFIER,
  INTERNAL_NAMESPACE_IDENTIFIER,
  MPACK_CACHE_DIR,
  MPACK_DATA_DIR,
  PRELUDE_PROTOCOL,
  REQUIRE_CONTEXT_PROTOCOL,
  RESOLVER_EXPORTS_MAP_CONDITIONS,
  RESOLVER_MAIN_FIELDS,
  SERVICE_BUNDLE_NAME,
  SHARED_BUNDLE_NAME,
  SOURCE_EXTENSIONS,
  VERSION
});
