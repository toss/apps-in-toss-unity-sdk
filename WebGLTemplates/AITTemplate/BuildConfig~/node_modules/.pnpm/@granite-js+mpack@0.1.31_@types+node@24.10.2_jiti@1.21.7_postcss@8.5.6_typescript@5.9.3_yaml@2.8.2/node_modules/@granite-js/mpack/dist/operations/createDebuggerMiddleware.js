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
var createDebuggerMiddleware_exports = {};
__export(createDebuggerMiddleware_exports, {
  createDebuggerMiddleware: () => createDebuggerMiddleware
});
module.exports = __toCommonJS(createDebuggerMiddleware_exports);
var import_path = __toESM(require("path"));
var import_readline = __toESM(require("readline"));
var import_devtools_frontend = __toESM(require("@granite-js/devtools-frontend"));
var import_prompts = require("@inquirer/prompts");
var import_chalk = __toESM(require("chalk"));
var import_connect = __toESM(require("connect"));
var import_debug = __toESM(require("debug"));
var import_serve_static = __toESM(require("serve-static"));
var import_constants = require("./constants");
var import_openDebugger = require("./openDebugger");
var import_vendors = require("../vendors");
const debug = (0, import_debug.default)("cli:start");
const { InspectorProxy } = (0, import_vendors.getModule)("metro-inspector-proxy");
const chromeInstanceMap = /* @__PURE__ */ new Map();
function createDebuggerMiddleware({ port, broadcastMessage }) {
  const middleware = (0, import_connect.default)().use(`/${import_constants.DEBUGGER_FRONTEND_PATH}`, (0, import_serve_static.default)(import_path.default.resolve(import_devtools_frontend.default)));
  function enableStdinWatchMode() {
    if (!process.stdout.isTTY || process.stdin.setRawMode == null) {
      console.warn("Watch mode is not supported in this environment");
      return;
    }
    import_readline.default.emitKeypressEvents(process.stdin);
    process.stdin.setRawMode(true);
    console.log(`To reload the app press ${import_chalk.default.blue('"r"')}`);
    console.log(`To open developer menu press ${import_chalk.default.blue('"d"')}`);
    console.log(`To open debugger press ${import_chalk.default.blue('"j"')}`);
    console.log("");
    process.stdin.on("keypress", (_key, data) => {
      if (data.ctrl === true) {
        switch (data.name) {
          case "c":
            process.exit(0);
          // eslint-disable-next-line no-fallthrough
          case "z":
            process.emit("SIGTSTP", "SIGTSTP");
            break;
        }
        return;
      }
      switch (data.name) {
        case "r":
          console.info("Reloading app...");
          broadcastMessage("reload");
          break;
        case "d":
          console.info("Opening developer menu...");
          broadcastMessage("devMenu");
          break;
        case "j":
          openReactNativeDebugger(port);
          break;
      }
    });
  }
  return { middleware, enableStdinWatchMode };
}
async function openReactNativeDebugger(port) {
  const connectedDevices = Array.from(InspectorProxy.devices.entries());
  let targetDevice;
  for (const [id, device] of connectedDevices) {
    debug(`[${id}] ${device.getName()}`);
  }
  if (connectedDevices.length === 0) {
    console.log("No compatible apps connected");
    return;
  } else if (connectedDevices.length === 1) {
    const [id, device] = connectedDevices[0];
    const name = device.getName();
    targetDevice = { id, name };
  } else {
    const deviceInfo = await (0, import_prompts.select)({
      message: "Select a device to connect",
      choices: connectedDevices.map(([id, device]) => ({
        value: { id, name: device.getName() },
        name: device.getName()
      }))
    });
    process.stdin.resume();
    targetDevice = deviceInfo;
  }
  console.log(`Opening debugger for '${targetDevice.name}'...`);
  chromeInstanceMap.get(targetDevice.id)?.kill();
  (0, import_openDebugger.openDebugger)(port, targetDevice.id.toString()).then((chrome) => {
    chromeInstanceMap.set(targetDevice.id, chrome);
  }).catch((error) => {
    if (error.message.includes("ECONNREFUSED")) {
      return;
    }
    console.error(error);
  });
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  createDebuggerMiddleware
});
