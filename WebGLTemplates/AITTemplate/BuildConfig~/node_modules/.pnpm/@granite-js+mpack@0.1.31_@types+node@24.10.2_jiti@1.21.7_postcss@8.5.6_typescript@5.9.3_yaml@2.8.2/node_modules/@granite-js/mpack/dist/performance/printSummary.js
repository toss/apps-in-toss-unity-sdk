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
var printSummary_exports = {};
__export(printSummary_exports, {
  printSummary: () => printSummary
});
module.exports = __toCommonJS(printSummary_exports);
var import_chalk = __toESM(require("chalk"));
function printSummary(summary, options) {
  const { limit = 30 } = options ?? {};
  Object.entries(summary).forEach(([category, result]) => {
    const records = result.records.sort((a, b) => b.duration - a.duration).slice(0, limit);
    console.log(
      `\u256D\u2500 Performance mark: ${import_chalk.default.blue.bold(category)} ${import_chalk.default.gray(`(Average duration: ${normalizeDuration(result.averageDuration)}ms)`)}`
    );
    records.forEach(({ sequence, duration, detail }, index) => {
      console.log(`\u2502 ${import_chalk.default.cyan(`Record #${index + 1}`)} (Duration: ${normalizeDuration(duration)}ms)`);
      Object.entries(detail ?? {}).forEach(([key, value]) => {
        console.log(`\u2502 \u251C\u2500 ${import_chalk.default.gray(`${key}: ${value}`)}`);
      });
      console.log(`\u2502 \u2570\u2500 Sequence: ${sequence}`);
    });
    console.log(`\u2570\u2500 Total records: ${result.records.length}`);
  });
}
function normalizeDuration(duration) {
  return duration.toFixed(8);
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  printSummary
});
