"use strict";
var __create = Object.create;
var __defProp = Object.defineProperty;
var __getOwnPropDesc = Object.getOwnPropertyDescriptor;
var __getOwnPropNames = Object.getOwnPropertyNames;
var __getProtoOf = Object.getPrototypeOf;
var __hasOwnProp = Object.prototype.hasOwnProperty;
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
var import_ws = __toESM(require("ws"));
module.exports = function createWebsocketServer({ websocketServer }) {
  const wss = new import_ws.default.Server({
    noServer: true
  });
  wss.on("connection", async (ws2, req) => {
    let connected = true;
    const url = req.url;
    const sendFn = (...args) => {
      if (connected) {
        ws2.send(...args);
      }
    };
    const client = await websocketServer.onClientConnect(url, sendFn);
    if (client == null) {
      ws2.close();
      return;
    }
    ws2.on("error", (e) => {
      websocketServer.onClientError && websocketServer.onClientError(client, e);
    });
    ws2.on("close", () => {
      websocketServer.onClientDisconnect && websocketServer.onClientDisconnect(client);
      connected = false;
    });
    ws2.on("message", (message) => {
      websocketServer.onClientMessage && websocketServer.onClientMessage(client, message, sendFn);
    });
  });
  return wss;
};
