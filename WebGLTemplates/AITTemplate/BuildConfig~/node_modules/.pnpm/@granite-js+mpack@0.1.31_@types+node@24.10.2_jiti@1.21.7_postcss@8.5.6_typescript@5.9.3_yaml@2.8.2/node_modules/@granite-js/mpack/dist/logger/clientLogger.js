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
var clientLogger_exports = {};
__export(clientLogger_exports, {
  clientLogger: () => clientLogger
});
module.exports = __toCommonJS(clientLogger_exports);
var import_Logger = require("./Logger");
const clientLogger = (level, data) => {
  switch (level) {
    case "trace":
      import_Logger.logger.trace(...data);
      break;
    case "debug":
      import_Logger.logger.debug(...data);
      break;
    case "info":
      import_Logger.logger.info(...data);
      break;
    case "warn":
      import_Logger.logger.warn(...data);
      break;
    case "error":
      import_Logger.logger.error(...data);
      break;
    default:
      import_Logger.logger.log(...data);
  }
};
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  clientLogger
});
