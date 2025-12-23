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
var openDebugger_exports = {};
__export(openDebugger_exports, {
  openDebugger: () => openDebugger
});
module.exports = __toCommonJS(openDebugger_exports);
var fs = __toESM(require("fs/promises"));
var os = __toESM(require("os"));
var path = __toESM(require("path"));
var import_chrome_launcher = require("chrome-launcher");
var import_constants = require("./constants");
const DEBUGGER_HOST = "localhost";
async function openDebugger(port, deviceId) {
  const appUrl = getDevToolsFrontendUrl(DEBUGGER_HOST, port, deviceId);
  const tempDir = await createTemporaryDirectory();
  const chromePath = (0, import_chrome_launcher.getChromePath)();
  if (!chromePath) {
    throw new Error("unable to get Chrome browser path");
  }
  return (0, import_chrome_launcher.launch)({
    chromePath,
    chromeFlags: [
      `--app=${appUrl}`,
      `--user-data-dir=${tempDir}`,
      "--no-first-run",
      "--no-default-browser-check",
      "--window-size=1200,800"
    ]
  });
}
function getDevToolsFrontendUrl(host, port, deviceId) {
  const wsUrl = `${host}:${port}/inspector/debug?device=${deviceId}&page=-1`;
  const url = new URL(`http://${host}:${port}/${import_constants.DEBUGGER_FRONTEND_PATH}/${import_constants.REACT_NATIVE_INSPECTOR_PAGE}`);
  url.searchParams.set("ws", wsUrl);
  url.searchParams.set("unstable_enableNetworkPanel", "true");
  url.searchParams.set("sources.hide_add_folder", "true");
  return url.toString();
}
async function createTemporaryDirectory() {
  const tempDir = path.join(os.tmpdir(), import_constants.DEBUGGER_TEMP_DIR);
  await fs.mkdir(tempDir, { recursive: true });
  return tempDir;
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  openDebugger
});
