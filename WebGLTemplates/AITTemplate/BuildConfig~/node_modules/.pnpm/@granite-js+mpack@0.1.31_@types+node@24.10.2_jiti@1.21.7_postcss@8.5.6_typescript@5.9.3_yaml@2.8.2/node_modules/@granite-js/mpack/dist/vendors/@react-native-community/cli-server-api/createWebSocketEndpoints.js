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
var createWebSocketEndpoints_exports = {};
__export(createWebSocketEndpoints_exports, {
  createWebSocketEndpoints: () => createWebSocketEndpoints
});
module.exports = __toCommonJS(createWebSocketEndpoints_exports);
var import_createDebuggerProxyEndpoint = __toESM(require("@react-native-community/cli-server-api/build/websocket/createDebuggerProxyEndpoint"));
var import_createEventsSocketEndpoint = __toESM(require("@react-native-community/cli-server-api/build/websocket/createEventsSocketEndpoint"));
var import_createMessageSocketEndpoint = __toESM(require("@react-native-community/cli-server-api/build/websocket/createMessageSocketEndpoint"));
function createWebSocketEndpoints(options) {
  return {
    debuggerProxySocket: (0, import_createDebuggerProxyEndpoint.default)(),
    eventsSocket: (0, import_createEventsSocketEndpoint.default)((method, params) => options.broadcast(method, params)),
    messageSocket: (0, import_createMessageSocketEndpoint.default)()
  };
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  createWebSocketEndpoints
});
