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
var createLiveReloadMiddleware_exports = {};
__export(createLiveReloadMiddleware_exports, {
  createLiveReloadMiddleware: () => createLiveReloadMiddleware
});
module.exports = __toCommonJS(createLiveReloadMiddleware_exports);
var ws = __toESM(require("ws"));
var import_logger = require("../../logger");
const getReloadByDevSettingsProxy = () => `(function () {
  var moduleName = "DevSettings";
  (window.__turboModuleProxy
    ? window.__turboModuleProxy(moduleName)
    : window.nativeModuleProxy[moduleName]).reload();
})();`;
const getMessage = (data) => {
  try {
    const parsedData = JSON.parse(String(data));
    return "type" in parsedData ? parsedData : null;
  } catch {
    return null;
  }
};
const createLiveReloadMiddleware = ({ onClientLog }) => {
  const server = new ws.WebSocketServer({ noServer: true });
  let connectedSocket = null;
  const handleClose = () => {
    connectedSocket = null;
  };
  const handleMessage = (event) => {
    const message = getMessage(event.data);
    if (!message) {
      return;
    }
    switch (message.type) {
      case "log": {
        onClientLog?.({
          type: "client_log",
          level: message.level,
          data: message.data,
          mode: "BRIDGE"
        });
        break;
      }
      // Not supported
      case "register-entrypoints":
      case "log-opt-in":
        break;
    }
  };
  const handleError = (error) => {
    if (error) {
      import_logger.logger.error("HMR update \uBA54\uC2DC\uC9C0\uB97C \uC804\uC1A1\uD560 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4", error);
    }
  };
  const liveReload = () => {
    const hmrUpdateMessage = {
      type: "update",
      body: {
        added: [
          {
            module: [-1, getReloadByDevSettingsProxy()],
            sourceMappingURL: null,
            sourceURL: null
          }
        ],
        deleted: [],
        modified: [],
        isInitialUpdate: false,
        revisionId: ""
      }
    };
    connectedSocket?.send(JSON.stringify(hmrUpdateMessage), handleError);
  };
  const updateStart = () => {
    const hmrUpdateStartMessage = {
      type: "update-start",
      body: {
        isInitialUpdate: false
      }
    };
    connectedSocket?.send(JSON.stringify(hmrUpdateStartMessage), handleError);
  };
  const updateDone = () => {
    const hmrUpdateDoneMessage = { type: "update-done" };
    connectedSocket?.send(JSON.stringify(hmrUpdateDoneMessage), handleError);
  };
  server.on("connection", (socket) => {
    connectedSocket = socket;
    connectedSocket.onerror = handleClose;
    connectedSocket.onclose = handleClose;
    connectedSocket.onmessage = handleMessage;
    import_logger.logger.debug("HMR \uC6F9\uC18C\uCF13 \uC5F0\uACB0\uB428");
  });
  server.on("error", (error) => {
    import_logger.logger.error("HMR \uC6F9\uC18C\uCF13 \uC11C\uBC84 \uC5D0\uB7EC", error);
  });
  return { server, liveReload, updateStart, updateDone };
};
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  createLiveReloadMiddleware
});
