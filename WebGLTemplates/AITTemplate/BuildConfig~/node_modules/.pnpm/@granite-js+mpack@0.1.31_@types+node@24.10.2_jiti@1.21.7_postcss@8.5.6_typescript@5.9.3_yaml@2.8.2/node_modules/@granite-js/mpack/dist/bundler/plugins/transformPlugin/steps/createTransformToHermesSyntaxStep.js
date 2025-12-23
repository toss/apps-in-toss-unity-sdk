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
var createTransformToHermesSyntaxStep_exports = {};
__export(createTransformToHermesSyntaxStep_exports, {
  createTransformToHermesSyntaxStep: () => createTransformToHermesSyntaxStep
});
module.exports = __toCommonJS(createTransformToHermesSyntaxStep_exports);
var import_path = __toESM(require("path"));
var swc = __toESM(require("@swc/core"));
var import_es_toolkit = require("es-toolkit");
var import_defineStepName = require("../../../../utils/defineStepName");
var import_swc = require("../../shared/swc");
function getParserConfig(filepath) {
  return /\.tsx?$/.test(filepath) ? {
    syntax: "typescript",
    tsx: true,
    dynamicImport: true
  } : {
    syntax: "ecmascript",
    jsx: true,
    exportDefaultFrom: true
  };
}
function createTransformToHermesSyntaxStep({
  dev,
  additionalSwcOptions = {}
}) {
  const plugins = (additionalSwcOptions.plugins ?? []).filter(import_es_toolkit.isNotNil);
  const transformToHermesSyntaxStep = async function transformToHermesSyntax(code, args) {
    const options = {
      minify: false,
      isModule: true,
      jsc: {
        ...import_swc.swcHelperOptimizationRules.jsc,
        parser: getParserConfig(args.path),
        target: "es5",
        keepClassNames: true,
        transform: {
          react: {
            runtime: "automatic",
            development: dev
          }
        },
        experimental: { plugins },
        loose: false,
        /**
         * 타입정의가 없지만 실제로는 동작하는 것이 스펙
         *
         * @see {@link https://github.com/swc-project/swc/blob/v1.4.10/crates/swc_ecma_transforms_base/src/assumptions.rs#L11}
         */
        // eslint-disable-next-line @typescript-eslint/ban-ts-comment
        // @ts-ignore
        assumptions: {
          setPublicClassFields: true,
          privateFieldsAsProperties: true
        }
      },
      /**
       * False error 로그가 찍히고 있어 비활성화
       */
      inputSourceMap: false,
      sourceMaps: "inline",
      filename: import_path.default.basename(args.path)
    };
    const result = await swc.transform(code, options);
    return { code: result.code };
  };
  (0, import_defineStepName.defineStepName)(transformToHermesSyntaxStep, "hermes-syntax");
  return transformToHermesSyntaxStep;
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  createTransformToHermesSyntaxStep
});
