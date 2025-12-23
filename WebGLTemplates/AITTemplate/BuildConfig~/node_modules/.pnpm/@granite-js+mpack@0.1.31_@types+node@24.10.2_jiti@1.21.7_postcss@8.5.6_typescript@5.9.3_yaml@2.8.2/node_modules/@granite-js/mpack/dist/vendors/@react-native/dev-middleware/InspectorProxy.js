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
var InspectorProxy_exports = {};
__export(InspectorProxy_exports, {
  InspectorProxy: () => InspectorProxy
});
module.exports = __toCommonJS(InspectorProxy_exports);
var import_url = __toESM(require("url"));
var import_dev_middleware = require("@react-native/dev-middleware");
var ws = __toESM(require("ws"));
var import_Device = require("./Device");
var import_logger = require("../../../logger");
const experiments = {
  enableNewDebugger: false,
  enableNetworkInspector: false,
  enableOpenDebuggerRedirect: false
};
const PAGES_LIST_JSON_URL = "/json";
const PAGES_LIST_JSON_URL_2 = "/json/list";
const PAGES_LIST_JSON_VERSION_URL = "/json/version";
const INTERNAL_ERROR_CODE = 1011;
class InspectorProxy extends import_dev_middleware.unstable_InspectorProxy {
  constructor({ root, serverBaseUrl }) {
    super(root, serverBaseUrl, null, experiments);
  }
  /**
   * 커스텀 Device 를 사용하기 위해 `InspectorProxy.createWebSocketListeners` 를 재구성한 메소드
   */
  createWebSocketServers({
    onDeviceWebSocketConnected,
    onDebuggerWebSocketConnected
  }) {
    return {
      deviceSocketServer: this.createDeviceWebSocketServer(onDeviceWebSocketConnected),
      debuggerSocketServer: this.createDebuggerWebSocketServer(onDebuggerWebSocketConnected)
    };
  }
  /**
   * Fastify 에서 사용할 수 있도록 `InspectorProxy.processRequest` 의 인터페이스를 재구성한 메소드
   */
  handleRequest(request, reply, done) {
    const { pathname } = import_url.default.parse(request.url);
    switch (pathname) {
      case PAGES_LIST_JSON_URL:
      case PAGES_LIST_JSON_URL_2:
        this.sendJsonResponse(reply, this.getPageDescriptions());
        break;
      case PAGES_LIST_JSON_VERSION_URL:
        this.sendJsonResponse(reply, { Browser: "Mobile JavaScript", "Protocol-Version": "1.1" });
        break;
    }
    done();
  }
  /**
   * 토스 커스텀 디버거를 띄우기 위해 내부 devices 를 노출시켜야 함
   */
  getDevices() {
    return this._devices;
  }
  sendJsonResponse(reply, object) {
    const data = JSON.stringify(object, null, 2);
    reply.status(200).headers({
      "Content-Type": "application/json; charset=UTF-8",
      "Cache-Control": "no-cache",
      "Content-Length": data.length.toString(),
      Connection: "close"
    }).send(data);
  }
  createDeviceWebSocketServer(onConnected) {
    const wss = new ws.WebSocketServer({
      noServer: true,
      perMessageDeflate: true
    });
    wss.on("connection", async (socket, request) => {
      try {
        const fallbackDeviceId = String(this._deviceCounter++);
        const query = import_url.default.parse(request.url || "", true).query || {};
        const deviceId = query.device || fallbackDeviceId;
        const deviceName = query.name || "Unknown";
        const appName = query.app || "Unknown";
        import_logger.logger.trace("Device \uC18C\uCF13 \uC5F0\uACB0\uB428", { deviceId, deviceName, appName });
        if (Array.isArray(deviceId) || Array.isArray(deviceName) || Array.isArray(appName)) {
          return;
        }
        const oldDevice = this._devices.get(deviceId);
        const newDevice = new import_Device.Device(deviceId, deviceName, appName, socket, this._projectRoot);
        if (oldDevice) {
          oldDevice.handleDuplicateDeviceConnection(newDevice);
        }
        this._devices.set(deviceId, newDevice);
        socket.on("close", () => {
          import_logger.logger.trace("Device \uC18C\uCF13 \uC5F0\uACB0 \uB04A\uAE40", { deviceId });
          this._devices.delete(deviceId);
        });
        onConnected(socket);
      } catch (error) {
        const errorMessage = error?.toString() ?? "Unknown error";
        import_logger.logger.error("Device \uC18C\uCF13 \uC5D0\uB7EC", errorMessage);
        socket.close(INTERNAL_ERROR_CODE, errorMessage);
      }
    });
    return wss;
  }
  createDebuggerWebSocketServer(onConnected) {
    const wss = new ws.WebSocketServer({
      noServer: true,
      perMessageDeflate: false,
      maxPayload: 0
    });
    wss.on("connection", async (socket, request) => {
      try {
        const query = import_url.default.parse(request.url || "", true).query || {};
        const deviceId = query.device;
        const pageId = query.page;
        const userAgent = query.userAgent;
        import_logger.logger.trace("Debugger \uC18C\uCF13 \uC5F0\uACB0\uB428", { deviceId, pageId, userAgent });
        if (deviceId == null || pageId == null) {
          throw new Error("\uCEE4\uB125\uC158 \uC694\uCCAD\uC5D0 device, page \uB9E4\uAC1C\uBCC0\uC218\uAC00 \uC874\uC7AC\uD574\uC57C \uD569\uB2C8\uB2E4");
        }
        if (Array.isArray(deviceId) || Array.isArray(pageId) || Array.isArray(userAgent)) {
          return;
        }
        const device = this._devices.get(deviceId);
        if (device == null) {
          throw new Error(`${deviceId} \uC5D0 \uD574\uB2F9\uD558\uB294 \uAE30\uAE30\uB97C \uCC3E\uC744 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4`);
        }
        device.handleDebuggerConnection(socket, pageId, {
          userAgent: request.headers["user-agent"] ?? userAgent ?? null
        });
        onConnected(socket);
      } catch (error) {
        const errorMessage = error?.toString() ?? "Unknown error";
        import_logger.logger.error("Debugger \uC18C\uCF13 \uC5D0\uB7EC", errorMessage);
        socket.close(INTERNAL_ERROR_CODE, errorMessage);
      }
    });
    return wss;
  }
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  InspectorProxy
});
