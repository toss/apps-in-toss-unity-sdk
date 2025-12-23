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
var DebuggerEventHandler_exports = {};
__export(DebuggerEventHandler_exports, {
  DebuggerEventHandler: () => DebuggerEventHandler
});
module.exports = __toCommonJS(DebuggerEventHandler_exports);
var import_debug = __toESM(require("debug"));
var import_parseDomain = require("./parseDomain");
const debug = (0, import_debug.default)("dev-server:debugger");
class DebuggerEventHandler {
  constructor(delegate) {
    this.delegate = delegate;
  }
  networkResponseData = /* @__PURE__ */ new Map();
  setDeviceWebSocketHandler(socket) {
    socket.on("message", (message) => this.handleDeviceMessage(socket, message));
  }
  setDebuggerWebSocketHandler(socket) {
    socket.on("message", (message) => this.handleDebuggerMessage(socket, message));
  }
  /**
   * 네이티브로부터 전달받은 wrappedEvent가 커스텀 이벤트인 경우 파싱하여 반환
   *
   * ```js
   * // message
   * {
   *   event: 'wrappedEvent',
   *   payload: {
   *     wrappedEvent: '<stringified event string>'
   *   }
   * }
   *
   * // wrappedEvent
   * {
   *   method: '<Namespace>.*',
   *   params: {}
   * }
   * ```
   */
  safetyParseCustomEvent(message) {
    try {
      const parsedMessage = JSON.parse(message.toString());
      if (parsedMessage.event === "wrappedEvent") {
        const wrappedEventPayload = JSON.parse(parsedMessage?.payload?.wrappedEvent);
        const domain = (0, import_parseDomain.parseDomain)(wrappedEventPayload.method);
        if (domain === "Bedrock" || domain === "Granite") {
          return wrappedEventPayload;
        }
      }
    } catch {
    }
    return null;
  }
  /**
   * Chrome Devtools Frontend 로부터 전달받은 CDP 이벤트 요청 데이터인 경우 파싱하여 반환
   *
   * ```js
   * {
   *   method: 'CDP Event',
   *   params: {}
   * }
   * ```
   */
  safetyParseDebuggerEvent(message) {
    try {
      return JSON.parse(message.toString());
    } catch {
      return null;
    }
  }
  handleDeviceMessage(socket, message) {
    const customEvent = this.safetyParseCustomEvent(message);
    if (customEvent == null) {
      return;
    }
    debug("handleDeviceMessage", customEvent);
    if (this.delegate?.onDeviceMessage?.(customEvent, socket)) {
      return;
    }
    switch (customEvent.method) {
      case "Bedrock.networkResponseData":
      case "Granite.networkResponseData":
        this.handleNetworkResponseData(customEvent);
        return;
    }
  }
  handleDebuggerMessage(socket, message) {
    let handled;
    const debuggerEvent = this.safetyParseDebuggerEvent(message);
    if (debuggerEvent == null) {
      return;
    }
    if (this.delegate?.onDebuggerMessage?.(debuggerEvent, socket)) {
      handled = true;
    } else {
      switch (debuggerEvent.method) {
        case "Network.getResponseBody":
          this.handleGetResponseBody(socket, debuggerEvent);
          handled = true;
          break;
        default:
          handled = false;
      }
    }
    if (handled) {
      debug("handleDebuggerMessage", debuggerEvent);
    }
  }
  handleNetworkResponseData(event) {
    const { params } = event;
    if (typeof params.requestId === "string") {
      this.networkResponseData.set(params.requestId, {
        data: params.data,
        base64Encoded: params.base64Encoded
      });
    }
  }
  handleGetResponseBody(socket, event) {
    const requestId = event.params.requestId.toString();
    const responseData = this.networkResponseData.get(requestId);
    if (responseData == null) {
      return;
    }
    this.networkResponseData.delete(requestId);
    let base64Encoded = responseData.base64Encoded;
    let parsedOriginalData = null;
    try {
      parsedOriginalData = JSON.parse(responseData.base64Encoded ? atob(responseData.data) : responseData.data);
      base64Encoded = false;
    } catch {
    }
    debug("handleGetResponseBody", responseData);
    socket.send(
      JSON.stringify({
        id: event.id,
        result: {
          /**
           * JSON 파싱 시도하여 실제로 JSON 형태의 값인지 확인
           *
           * - 파싱 성공: 파싱된 데이터 전송
           * - 파싱 실패: 기존 데이터 그대로 전송
           */
          body: typeof parsedOriginalData === "object" ? JSON.stringify(parsedOriginalData) : responseData.data,
          base64Encoded
        }
      })
    );
  }
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  DebuggerEventHandler
});
