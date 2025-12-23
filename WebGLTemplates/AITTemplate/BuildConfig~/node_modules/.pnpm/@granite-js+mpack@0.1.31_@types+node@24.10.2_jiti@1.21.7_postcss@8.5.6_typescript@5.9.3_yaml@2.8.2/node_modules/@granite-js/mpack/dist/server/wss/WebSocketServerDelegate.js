"use strict";
var __defProp = Object.defineProperty;
var __getOwnPropDesc = Object.getOwnPropertyDescriptor;
var __getOwnPropNames = Object.getOwnPropertyNames;
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
var __toCommonJS = (mod) => __copyProps(__defProp({}, "__esModule", { value: true }), mod);
var WebSocketServerDelegate_exports = {};
__export(WebSocketServerDelegate_exports, {
  WebSocketServerDelegate: () => WebSocketServerDelegate
});
module.exports = __toCommonJS(WebSocketServerDelegate_exports);
class WebSocketServerDelegate {
  constructor(delegateParams) {
    this.delegateParams = delegateParams;
  }
  sendEvent(event) {
    this.delegateParams.eventReporter(event);
  }
  broadcastCommand(command, params) {
    this.delegateParams.messageBroadcaster(command, params);
  }
  onHMRUpdateStart() {
    this.delegateParams.hmr.updateStart();
  }
  onHMRUpdateDone() {
    this.delegateParams.hmr.updateDone();
  }
  /**
   * @TODO: HMR 구현 필요 (대신 실시간 새로고침 제공)
   */
  hotReload() {
    this.delegateParams.hmr.reload();
  }
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  WebSocketServerDelegate
});
