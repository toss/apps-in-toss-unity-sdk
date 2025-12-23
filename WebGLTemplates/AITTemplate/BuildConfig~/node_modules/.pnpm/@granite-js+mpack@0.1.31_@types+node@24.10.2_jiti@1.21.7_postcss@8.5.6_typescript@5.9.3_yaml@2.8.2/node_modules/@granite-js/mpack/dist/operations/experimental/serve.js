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
var serve_exports = {};
__export(serve_exports, {
  EXPERIMENTAL__server: () => EXPERIMENTAL__server
});
module.exports = __toCommonJS(serve_exports);
var import_plugin_core = require("@granite-js/plugin-core");
var import_prompts = require("@inquirer/prompts");
var import_debug = __toESM(require("debug"));
var import_StartMenuHandler = require("./StartMenuHandler");
var import_constants = require("../../constants");
var import_DevServer = require("../../server/DevServer");
var import_printLogo = require("../../utils/printLogo");
var import_openDebugger = require("../openDebugger");
const debug = (0, import_debug.default)("cli:start");
const chromeInstanceMap = /* @__PURE__ */ new Map();
async function EXPERIMENTAL__server({
  config,
  host = import_constants.DEV_SERVER_DEFAULT_HOST,
  port = import_constants.DEV_SERVER_DEFAULT_PORT,
  onServerReady
}) {
  const driver = (0, import_plugin_core.createPluginHooksDriver)(config);
  await driver.devServer.pre({ host, port });
  const rootDir = config.cwd;
  const { metro: _, devServer, ...buildConfig } = await (0, import_plugin_core.resolveConfig)(config) ?? {};
  const server = new import_DevServer.DevServer({
    buildConfig: { entry: config.entryFile, ...buildConfig },
    middlewares: devServer?.middlewares ?? [],
    host,
    port,
    rootDir
  });
  (0, import_printLogo.printLogo)();
  await server.initialize();
  await server.listen();
  await driver.devServer.post({ host, port });
  await onServerReady?.();
  const menuHandler = new import_StartMenuHandler.StartMenuHandler([
    {
      key: "r",
      description: "Refresh",
      action: () => {
        console.log("Refreshing...");
        server.broadcastCommand("reload");
      }
    },
    {
      key: "d",
      description: "Open Developer Menu",
      action: () => {
        console.log("Opening developer menu...");
        server.broadcastCommand("devMenu");
      }
    },
    {
      key: "j",
      description: "Open Debugger",
      action: async () => {
        const devices = server.getInspectorProxy()?.getDevices();
        const connectedDevices = Array.from(devices?.entries() ?? []);
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
        (0, import_openDebugger.openDebugger)(server.port, targetDevice.id).then((chrome) => {
          chromeInstanceMap.set(targetDevice.id, chrome);
        }).catch((error) => {
          if (error.message.includes("ECONNREFUSED")) {
            return;
          }
          console.error(error);
        });
      }
    }
  ]).attach();
  return {
    cleanup: async () => {
      await server.close();
      menuHandler.close();
    }
  };
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  EXPERIMENTAL__server
});
