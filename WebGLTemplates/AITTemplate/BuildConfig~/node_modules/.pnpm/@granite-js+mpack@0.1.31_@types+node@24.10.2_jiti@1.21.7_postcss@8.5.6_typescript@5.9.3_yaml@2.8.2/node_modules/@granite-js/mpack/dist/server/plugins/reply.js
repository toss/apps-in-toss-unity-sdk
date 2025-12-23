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
var reply_exports = {};
__export(reply_exports, {
  invalidRequest: () => invalidRequest,
  notFound: () => notFound
});
module.exports = __toCommonJS(reply_exports);
var import_Logger = require("../../logger/Logger");
function invalidRequest(reply, logMessage) {
  import_Logger.logger.warn(`invalid request: ${logMessage}`);
  return reply.code(400);
}
function notFound(reply, logMessage) {
  import_Logger.logger.warn(`not found: ${logMessage}`);
  return reply.code(404);
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  invalidRequest,
  notFound
});
