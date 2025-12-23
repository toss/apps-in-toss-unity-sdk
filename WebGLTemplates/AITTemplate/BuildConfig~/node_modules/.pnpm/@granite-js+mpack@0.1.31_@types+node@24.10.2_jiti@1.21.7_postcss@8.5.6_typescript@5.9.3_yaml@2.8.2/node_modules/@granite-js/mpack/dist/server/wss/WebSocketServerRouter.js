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
var WebSocketServerRouter_exports = {};
__export(WebSocketServerRouter_exports, {
  WebSocketServerRouter: () => WebSocketServerRouter
});
module.exports = __toCommonJS(WebSocketServerRouter_exports);
var url = __toESM(require("url"));
class WebSocketServerRouter {
  webSocketServers = /* @__PURE__ */ new Map();
  register(path, wss) {
    this.webSocketServers.set(path, wss);
    return this;
  }
  setup(app) {
    app.server.on("upgrade", this.onUpgrade.bind(this));
  }
  onUpgrade(request, socket, head) {
    if (!request.url) {
      return;
    }
    let upgraded = false;
    const { pathname } = url.parse(request.url);
    for (const [path, wss] of this.webSocketServers.entries()) {
      if (path === pathname) {
        wss.handleUpgrade(request, socket, head, (client) => {
          wss.emit("connection", client, request);
        });
        upgraded = true;
      }
    }
    if (!upgraded) {
      socket.destroy();
    }
  }
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  WebSocketServerRouter
});
