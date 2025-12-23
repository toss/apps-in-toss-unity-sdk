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
var progressBar_exports = {};
__export(progressBar_exports, {
  cleanup: () => cleanup,
  createProgressBar: () => createProgressBar
});
module.exports = __toCommonJS(progressBar_exports);
var Frogress = __toESM(require("frogress-bar"));
const PROGRESS_TEMPLATE = "{status} {progress} ({value}/{total}) | {label}";
const STATUS_SYMBOLS = {
  waiting: "\u2026",
  running: "\u25CF",
  warning: "!",
  error: "\u2715",
  success: "\u2713"
};
function createProgressBar(label) {
  const progressBar = Frogress.create({
    template: PROGRESS_TEMPLATE,
    total: 0,
    placeholder: {
      label: Frogress.color(label, "gray"),
      status: Frogress.color(STATUS_SYMBOLS.waiting, "gray")
    }
  });
  return {
    start: (moduleCount, totalModuleCount) => {
      progressBar.start({
        value: moduleCount,
        total: totalModuleCount
      });
    },
    update: (moduleCount, totalModuleCount) => {
      progressBar.update({
        value: moduleCount,
        total: totalModuleCount ?? moduleCount,
        placeholder: {
          status: Frogress.color(STATUS_SYMBOLS.running, "gray")
        }
      });
    },
    done: (result) => {
      const { value } = progressBar.getState();
      let status;
      switch (true) {
        case result.errors.length > 0:
          status = Frogress.color(STATUS_SYMBOLS.error, "red");
          break;
        case result.warnings.length > 0:
          status = Frogress.color(STATUS_SYMBOLS.warning, "yellow");
          break;
        default:
          status = Frogress.color(STATUS_SYMBOLS.success, "blue");
      }
      progressBar.update({
        value,
        placeholder: { status }
      });
    },
    remove: () => {
      Frogress.remove(progressBar);
    },
    hide: () => {
      progressBar.stop();
    }
  };
}
function cleanup() {
  Frogress.removeAll();
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  cleanup,
  createProgressBar
});
