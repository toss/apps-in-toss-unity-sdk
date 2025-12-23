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
import * as ws from "ws";
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
export {
  DEFAULT_HOST,
  DEFAULT_PROXY_WSS_PORT,
  ProxyEventType,
  RN_WSS_PORT,
  setupDevToolsProxy
};
