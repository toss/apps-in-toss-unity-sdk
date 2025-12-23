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

// src/backend/index.ts
var backend_exports = {};
__export(backend_exports, {
  DEFAULT_HOST: () => DEFAULT_HOST,
  DEFAULT_PROXY_WSS_PORT: () => DEFAULT_PROXY_WSS_PORT,
  ProxyEventType: () => ProxyEventType,
  RN_WSS_PORT: () => RN_WSS_PORT,
  setupDevToolsProxy: () => setupDevToolsProxy
});
module.exports = __toCommonJS(backend_exports);

// src/shared/index.ts
var RN_WSS_PORT = 8097;
var DEFAULT_PROXY_WSS_PORT = 8098;
var DEFAULT_HOST = "localhost";
var ProxyEventType = /* @__PURE__ */ ((ProxyEventType2) => {
  ProxyEventType2["OPEN"] = "open";
  ProxyEventType2["CLOSE"] = "close";
  return ProxyEventType2;
})(ProxyEventType || {});

// src/backend/proxy-websocket.ts
var ws = __toESM(require("ws"));
var ProxyWebSocket = class {
  constructor({ host, port, delegate }) {
    const wss = new ws.WebSocketServer({ host, port });
    wss.on("error", (error) => delegate?.onError?.(error));
    wss.on("connection", (ws2) => {
      this.onConnect(ws2);
      ws2.on("close", this.onClose.bind(this));
      ws2.on("message", this.onMessage.bind(this));
      ws2.on("error", (error) => delegate?.onError?.(error));
    });
    this.wss = wss;
    this.delegate = delegate;
  }
  createProxyEvent(event) {
    return { event, __isProxy: true };
  }
  onConnect(socket) {
    const event = this.createProxyEvent("open" /* OPEN */);
    const isHandled = this.delegate?.onConnect?.({
      socket,
      proxyWebSocket: this.proxyWebSocket
    });
    !isHandled && this.proxyWebSocket?.send(JSON.stringify(event));
  }
  onClose() {
    const event = this.createProxyEvent("close" /* CLOSE */);
    const isHandled = this.delegate?.onClose?.({
      proxyWebSocket: this.proxyWebSocket
    });
    !isHandled && this.proxyWebSocket?.send(JSON.stringify(event));
  }
  onMessage(data) {
    const stringifiedData = data instanceof ArrayBuffer ? Buffer.from(data).toString() : data.toString();
    const isHandled = this.delegate?.onMessage?.({
      data: stringifiedData,
      proxyWebSocket: this.proxyWebSocket
    });
    !isHandled && this.proxyWebSocket?.send(stringifiedData);
  }
  send(data) {
    this.wss.clients.forEach((client) => {
      client.send(data);
    });
  }
  close() {
    this.unbind();
    return new Promise((resolve, reject) => {
      this.wss.close((error) => {
        if (error) {
          reject(error);
        } else {
          resolve();
        }
      });
    });
  }
  bind(proxyWebSocket) {
    if (this.proxyWebSocket) {
      throw new Error("already another proxy websocket server bound");
    }
    this.proxyWebSocket = proxyWebSocket;
  }
  unbind() {
    this.proxyWebSocket = void 0;
  }
};

// src/backend/backend.ts
var setupDevToolsProxy = (config) => {
  const { client = {}, devtools = {} } = config;
  const clientWebSocket = new ProxyWebSocket({
    host: client.host,
    port: client.port ?? RN_WSS_PORT,
    delegate: client.delegate
  });
  const devToolsWebSocket = new ProxyWebSocket({
    host: devtools.host ?? DEFAULT_HOST,
    port: devtools.port ?? DEFAULT_PROXY_WSS_PORT,
    delegate: devtools.delegate
  });
  clientWebSocket.bind(devToolsWebSocket);
  devToolsWebSocket.bind(clientWebSocket);
  return async function cleanup() {
    await clientWebSocket.close();
    await devToolsWebSocket.close();
  };
};
