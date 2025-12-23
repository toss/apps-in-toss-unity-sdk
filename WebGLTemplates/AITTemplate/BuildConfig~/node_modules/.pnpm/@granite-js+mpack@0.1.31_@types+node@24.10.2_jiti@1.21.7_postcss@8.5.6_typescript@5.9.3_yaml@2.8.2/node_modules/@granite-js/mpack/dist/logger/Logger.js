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
var Logger_exports = {};
__export(Logger_exports, {
  logger: () => logger
});
module.exports = __toCommonJS(Logger_exports);
var import_chalk = __toESM(require("chalk"));
var import_es_toolkit = require("es-toolkit");
var import_isDebugMode = require("../utils/isDebugMode");
class Logger {
  static LEVEL_COLOR = {
    trace: import_chalk.default.gray,
    debug: import_chalk.default.gray,
    info: import_chalk.default.cyan,
    log: import_chalk.default.green,
    warn: import_chalk.default.yellow,
    error: import_chalk.default.red
  };
  trace(...messages) {
    if ((0, import_isDebugMode.isDebugMode)("mpack")) {
      this.stdout(this.createLogString("trace", ...messages));
    }
  }
  debug(...messages) {
    if ((0, import_isDebugMode.isDebugMode)("mpack")) {
      this.stdout(this.createLogString("debug", ...messages));
    }
  }
  info(...messages) {
    this.stdout(this.createLogString("info", ...messages));
  }
  log(...messages) {
    this.stdout(this.createLogString("log", ...messages));
  }
  warn(...messages) {
    this.stderr(this.createLogString("warn", ...messages));
  }
  error(...messages) {
    this.stderr(this.createLogString("error", ...messages));
  }
  getTimestamp() {
    const date = /* @__PURE__ */ new Date();
    const yyyy = date.getFullYear();
    const MM = (date.getMonth() + 1).toString().padStart(2, "0");
    const dd = date.getDate().toString().padStart(2, "0");
    const hh = date.getHours().toString().padStart(2, "0");
    const mm = date.getMinutes().toString().padStart(2, "0");
    const ss = date.getSeconds().toString().padStart(2, "0");
    const sss = date.getMilliseconds().toString().padStart(3, "0");
    return `${yyyy}-${MM}-${dd} ${hh}:${mm}:${ss}.${sss}`;
  }
  stdout(buffer) {
    process.stdout.write(buffer);
  }
  stderr(buffer) {
    process.stderr.write(buffer);
  }
  stringify(value) {
    if (value instanceof Error) {
      return value.toString();
    }
    switch (typeof value) {
      case "string":
        return value;
      case "number":
      case "boolean":
        return value.toString();
      case "undefined":
        return "undefined";
      case "object":
        return JSON.stringify(value, null, 2);
      default:
        return null;
    }
  }
  createLogString(level, ...messages) {
    const timestamp = import_chalk.default.gray(this.getTimestamp());
    const coloredLevel = Logger.LEVEL_COLOR[level](import_chalk.default.bold(level));
    const message = messages.map(this.stringify).filter(import_es_toolkit.isNotNil).join(" ");
    return `${timestamp} ${coloredLevel} ${message}
`;
  }
}
const logger = new Logger();
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  logger
});
