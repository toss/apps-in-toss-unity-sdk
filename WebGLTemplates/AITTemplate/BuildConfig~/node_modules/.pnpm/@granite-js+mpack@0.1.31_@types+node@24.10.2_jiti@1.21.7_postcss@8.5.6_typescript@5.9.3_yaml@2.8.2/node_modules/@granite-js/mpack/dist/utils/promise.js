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
var promise_exports = {};
__export(promise_exports, {
  PromiseHandler: () => PromiseHandler,
  isFulfilled: () => isFulfilled,
  isRejected: () => isRejected
});
module.exports = __toCommonJS(promise_exports);
var import_es_toolkit = require("es-toolkit");
class PromiseHandler {
  constructor(revisionId) {
    this.revisionId = revisionId;
    this.task = new Promise((resolve, reject) => {
      this.resolver = resolve;
      this.rejector = reject;
    });
  }
  _isDone = false;
  task;
  resolver = import_es_toolkit.noop;
  rejector = import_es_toolkit.noop;
  get isDone() {
    return this._isDone;
  }
  wait() {
    return this.task;
  }
  done(result) {
    this._isDone = true;
    this.resolver(result);
  }
  abort(reason) {
    this.rejector(reason);
  }
}
function isFulfilled(task) {
  return task.status === "fulfilled";
}
function isRejected(task) {
  return task.status === "rejected";
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  PromiseHandler,
  isFulfilled,
  isRejected
});
