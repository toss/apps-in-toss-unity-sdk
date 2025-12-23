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
var StartMenuHandler_exports = {};
__export(StartMenuHandler_exports, {
  StartMenuHandler: () => StartMenuHandler
});
module.exports = __toCommonJS(StartMenuHandler_exports);
var import_readline = __toESM(require("readline"));
var import_chalk = __toESM(require("chalk"));
class StartMenuHandler {
  constructor(menus) {
    this.menus = menus;
  }
  attach() {
    const isSupportInteractiveMode = process.stdout.isTTY && typeof process.stdin.setRawMode === "function";
    if (isSupportInteractiveMode) {
      import_readline.default.emitKeypressEvents(process.stdin);
      process.stdin.setRawMode(true);
      process.stdin.setEncoding("utf8");
      process.stdin.on("keypress", this.keyPressHandler.bind(this));
      process.stdout.write("\n");
      this.menus.forEach(({ key, description }) => {
        console.log(`${import_chalk.default.bold(key)} - ${description}`);
      });
      process.stdout.write("\n");
    }
    return this;
  }
  close() {
    process.stdin.off("keypress", this.keyPressHandler);
  }
  keyPressHandler(_data, { ctrl, name }) {
    if (name === void 0) {
      console.log("\uD55C/\uC601\uD0A4\uB97C \uD655\uC778\uD574\uC8FC\uC138\uC694");
      return;
    }
    if (ctrl) {
      switch (name) {
        // Ctrl + C: SIGINT
        case "c":
          process.exit(0);
          return;
        // Ctrl + Z: SIGTSTP
        case "z":
          process.emit("SIGTSTP", "SIGTSTP");
          return;
      }
      return;
    }
    this.menus.forEach(({ key, action }) => {
      if (key === name) {
        action();
      }
    });
  }
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  StartMenuHandler
});
