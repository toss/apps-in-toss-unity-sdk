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
var createStripFlowStep_exports = {};
__export(createStripFlowStep_exports, {
  createStripFlowStep: () => createStripFlowStep
});
module.exports = __toCommonJS(createStripFlowStep_exports);
var import_path = __toESM(require("path"));
var babel = __toESM(require("@babel/core"));
var sucrase = __toESM(require("sucrase"));
var import_defineStepName = require("../../../../utils/defineStepName");
function createStripFlowStep(config) {
  const stripImportTypeofStatements = (code) => {
    return code.split("\n").filter((line) => !line.startsWith("import typeof ")).join("\n");
  };
  const stripFlowStep = async function stripFlow(code, args) {
    const shouldTransform = args.path.endsWith(".js");
    if (!shouldTransform) {
      return { code };
    }
    try {
      const result = sucrase.transform(code, {
        transforms: ["flow", "jsx"],
        jsxRuntime: "preserve",
        disableESTransforms: true
      });
      return { code: stripImportTypeofStatements(result.code) };
    } catch {
      const result = await babel.transformAsync(code, {
        configFile: false,
        minified: false,
        compact: false,
        babelrc: false,
        envName: config.dev ? "development" : "production",
        caller: {
          name: "mpack-strip-flow-plugin",
          supportsStaticESM: true
        },
        presets: [
          /**
           * flow 구문과 jsx 구문이 함께 존재하는 경우가 있기에 preset-react 사용
           */
          [require.resolve("@babel/preset-react"), { runtime: "automatic" }]
        ],
        plugins: [
          /**
           * flow 구문 변환을 위해 flow-strip-types 사용
           */
          require.resolve("@babel/plugin-transform-flow-strip-types")
        ],
        filename: import_path.default.basename(args.path)
      });
      if (result?.code != null) {
        return { code: result.code };
      }
      throw new Error("babel transform result is null");
    }
  };
  (0, import_defineStepName.defineStepName)(stripFlowStep, "strip-flow");
  return stripFlowStep;
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  createStripFlowStep
});
