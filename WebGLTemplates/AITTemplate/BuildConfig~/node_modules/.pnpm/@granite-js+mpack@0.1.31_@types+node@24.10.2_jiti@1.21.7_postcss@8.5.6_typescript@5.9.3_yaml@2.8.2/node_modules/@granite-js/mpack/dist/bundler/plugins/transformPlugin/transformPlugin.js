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
var transformPlugin_exports = {};
__export(transformPlugin_exports, {
  transformPlugin: () => transformPlugin
});
module.exports = __toCommonJS(transformPlugin_exports);
var import_assert = __toESM(require("assert"));
var fs = __toESM(require("fs/promises"));
var preludeScript = __toESM(require("./helpers/preludeScript"));
var import_createCacheSteps = require("./steps/createCacheSteps");
var import_createFullyTransformStep = require("./steps/createFullyTransformStep");
var import_createStripFlowStep = require("./steps/createStripFlowStep");
var import_createTransformToHermesSyntaxStep = require("./steps/createTransformToHermesSyntaxStep");
var import_performance = require("../../../performance");
var import_transformer = require("../../../transformer");
const sourceRegExp = /\.([mc]js|[tj]sx?)$/;
function transformPlugin({ context, ...options }) {
  return {
    name: "transform-plugin",
    setup(build) {
      const { id, config } = context;
      const { dev, cache, buildConfig } = config;
      const { esbuild, swc, babel } = buildConfig;
      (0, import_assert.default)(id, "id \uAC12\uC774 \uC874\uC7AC\uD558\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4");
      (0, import_assert.default)(typeof dev === "boolean", "dev \uAC12\uC774 \uC874\uC7AC\uD558\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4");
      (0, import_assert.default)(typeof cache === "boolean", "cache \uAC12\uC774 \uC874\uC7AC\uD558\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4");
      const cacheSteps = (0, import_createCacheSteps.createCacheSteps)({ id, enabled: cache });
      const transformPipeline = new import_transformer.AsyncTransformPipeline().beforeStep(cacheSteps.beforeTransform).addStep(async (code, args) => {
        if (options.transformSync) {
          code = options.transformSync(args.path, code);
        }
        if (options.transformAsync) {
          code = await options.transformAsync(args.path, code);
        }
        return { code };
      }).addStep((0, import_createFullyTransformStep.createFullyTransformStep)({ dev, additionalBabelOptions: babel }), {
        conditions: babel?.conditions,
        skipOtherSteps: true
      }).addStep((0, import_createStripFlowStep.createStripFlowStep)({ dev })).addStep((0, import_createTransformToHermesSyntaxStep.createTransformToHermesSyntaxStep)({ dev, additionalSwcOptions: swc })).afterStep(cacheSteps.afterTransform);
      preludeScript.registerEntryPointMarker(build);
      preludeScript.registerPreludeScriptResolver(build);
      build.onLoad({ filter: sourceRegExp }, async (args) => {
        let code = await fs.readFile(args.path, "utf-8");
        if (preludeScript.isEntryPoint(args)) {
          code = preludeScript.injectPreludeScript(code, {
            preludeScriptPaths: esbuild?.prelude ?? []
          });
        }
        const result = await import_performance.Performance.withTrace(() => transformPipeline.transform(code, args), {
          name: "transform",
          startOptions: { detail: { file: args.path } }
        });
        return { contents: result.code, loader: "js" };
      });
    }
  };
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  transformPlugin
});
