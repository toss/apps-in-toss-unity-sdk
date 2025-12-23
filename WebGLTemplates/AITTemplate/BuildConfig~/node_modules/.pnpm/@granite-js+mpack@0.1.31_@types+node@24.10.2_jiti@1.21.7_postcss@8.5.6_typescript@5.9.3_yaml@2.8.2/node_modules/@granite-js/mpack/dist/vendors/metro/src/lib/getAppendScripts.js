"use strict";
var __create = Object.create;
var __defProp = Object.defineProperty;
var __getOwnPropDesc = Object.getOwnPropertyDescriptor;
var __getOwnPropNames = Object.getOwnPropertyNames;
var __getProtoOf = Object.getPrototypeOf;
var __hasOwnProp = Object.prototype.hasOwnProperty;
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
var import_CountingSet = __toESM(require("./CountingSet"));
const getInlineSourceMappingURL = require("../DeltaBundler/Serializers/helpers/getInlineSourceMappingURL");
const sourceMapString = require("../DeltaBundler/Serializers/sourceMapString");
const countLines = require("./countLines");
const nullthrows = require("nullthrows");
const path = require("path");
function getAppendScripts(entryPoint, modules, importBundleNames, options) {
  const output = [];
  if (importBundleNames.size) {
    const importBundleNamesObject = /* @__PURE__ */ Object.create(null);
    importBundleNames.forEach((absolutePath) => {
      const bundlePath = path.relative(options.serverRoot, absolutePath);
      importBundleNamesObject[options.createModuleId(absolutePath)] = bundlePath.slice(
        0,
        -path.extname(bundlePath).length
      );
    });
    const code = `(function(){var $$=${options.getRunModuleStatement(
      options.createModuleId(options.asyncRequireModulePath)
    )}$$.addImportBundleNames(${String(JSON.stringify(importBundleNamesObject))})})();`;
    output.push({
      path: "$$importBundleNames",
      dependencies: /* @__PURE__ */ new Map(),
      getSource: () => Buffer.from(""),
      inverseDependencies: new import_CountingSet.default(),
      output: [
        {
          type: "js/script/virtual",
          data: {
            code,
            lineCount: countLines(code),
            map: []
          }
        }
      ]
    });
  }
  if (options.runModule) {
    const paths = [...options.runBeforeMainModule, entryPoint];
    for (const path2 of paths) {
      if (modules.some((module2) => module2.path === path2)) {
        const code = options.getRunModuleStatement(options.createModuleId(path2));
        output.push({
          path: `require-${path2}`,
          dependencies: /* @__PURE__ */ new Map(),
          getSource: () => Buffer.from(""),
          inverseDependencies: new import_CountingSet.default(),
          output: [
            {
              type: "js/script/virtual",
              data: {
                code,
                lineCount: countLines(code),
                map: []
              }
            }
          ]
        });
      }
    }
  }
  if (options.inlineSourceMap || options.sourceMapUrl) {
    const sourceMappingURL = options.inlineSourceMap ? getInlineSourceMappingURL(
      sourceMapString(modules, {
        processModuleFilter: () => true,
        excludeSource: false
      })
    ) : nullthrows(options.sourceMapUrl);
    const code = `//# sourceMappingURL=${sourceMappingURL}`;
    output.push({
      path: "source-map",
      dependencies: /* @__PURE__ */ new Map(),
      getSource: () => Buffer.from(""),
      inverseDependencies: new import_CountingSet.default(),
      output: [
        {
          type: "js/script/virtual",
          data: {
            code,
            lineCount: countLines(code),
            map: []
          }
        }
      ]
    });
  }
  if (options.sourceUrl) {
    const code = `//# sourceURL=${options.sourceUrl}`;
    output.push({
      path: "source-url",
      dependencies: /* @__PURE__ */ new Map(),
      getSource: () => Buffer.from(""),
      inverseDependencies: new import_CountingSet.default(),
      output: [
        {
          type: "js/script/virtual",
          data: {
            code,
            lineCount: countLines(code),
            map: []
          }
        }
      ]
    });
  }
  return output;
}
module.exports = getAppendScripts;
