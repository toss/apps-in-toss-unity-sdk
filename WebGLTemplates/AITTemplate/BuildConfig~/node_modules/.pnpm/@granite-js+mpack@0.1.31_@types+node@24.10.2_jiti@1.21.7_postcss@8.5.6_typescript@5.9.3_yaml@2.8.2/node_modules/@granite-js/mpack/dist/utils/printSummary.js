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
var import_es_toolkit = require("es-toolkit");
var import_esbuild = require("esbuild");
var import_logger = require("../logger");
var import_buildResult = require("./buildResult");
function printSummary(buildResult) {
  return Promise.all([
    (0, import_esbuild.formatMessages)(buildResult.warnings, { kind: "warning", color: true }),
    (0, import_esbuild.formatMessages)(buildResult.errors, { kind: "error", color: true })
  ]).then(([warnings, errors]) => {
    warnings.forEach((message) => console.warn("\n\n", message));
    errors.forEach((message) => console.error("\n\n", message));
    const platform = `[${buildResult.platform}]`;
    const moduleCountAndDuration = (0, import_buildResult.isBuildSuccess)(buildResult) ? import_chalk.default.underline(`${buildResult.totalModuleCount} Modules (${(buildResult.duration / 1e3).toFixed(2)}s)`) : null;
    const summary = [
      platform,
      moduleCountAndDuration,
      `${import_chalk.default.red(errors.length)} errors`,
      `${import_chalk.default.yellow(warnings.length)} warnings`
    ].filter(import_es_toolkit.isNotNil).join(import_chalk.default.white(" | "));
    import_logger.logger.info(import_chalk.default.gray(summary));
  });
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  printSummary
});
