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
var buildStatusPlugin_exports = {};
__export(buildStatusPlugin_exports, {
  buildStatusPlugin: () => buildStatusPlugin
});
module.exports = __toCommonJS(buildStatusPlugin_exports);
var import_logger = require("../../../logger");
var import_getBundleOutputs = require("../../../utils/getBundleOutputs");
var import_getSourcemapName = require("../../../utils/getSourcemapName");
function buildStatusPlugin({ context, ...hooks }) {
  return {
    name: "build-status-plugin",
    setup(build) {
      let buildStartedAt = 0;
      let moduleCount = 0;
      build.onStart(async () => {
        import_logger.logger.debug("Preparing build", { id: context.id });
        await hooks.onPrepare();
        moduleCount = 0;
        buildStartedAt = performance.now();
        import_logger.logger.debug("Build started", { id: context.id, buildStartedAt });
        await hooks.onStart();
      });
      build.onLoad({ filter: /.*/ }, async () => {
        await hooks.onLoad({ moduleCount: ++moduleCount });
        return null;
      });
      build.onEnd(async (result) => {
        const endAt = performance.now();
        const duration = endAt - buildStartedAt;
        const { buildConfig } = context.config;
        const { outfile, sourcemapOutfile, platform, extra } = buildConfig;
        const { source, sourcemap } = (0, import_getBundleOutputs.getBundleOutputs)(outfile, result);
        const buildResult = extendBuildResult(
          result,
          source && sourcemap ? {
            bundle: { source, sourcemap },
            outfile,
            sourcemapOutfile: sourcemapOutfile ?? (0, import_getSourcemapName.getSourcemapName)(outfile),
            platform,
            extra,
            duration,
            size: source.contents.byteLength,
            totalModuleCount: moduleCount
          } : { platform, extra, duration }
        );
        await hooks.onEnd(buildResult);
      });
    }
  };
}
function extendBuildResult(result, properties) {
  return Object.defineProperties(
    result,
    Object.fromEntries(Object.entries(properties).map(([property, value]) => [property, { value, enumerable: true }]))
  );
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  buildStatusPlugin
});
