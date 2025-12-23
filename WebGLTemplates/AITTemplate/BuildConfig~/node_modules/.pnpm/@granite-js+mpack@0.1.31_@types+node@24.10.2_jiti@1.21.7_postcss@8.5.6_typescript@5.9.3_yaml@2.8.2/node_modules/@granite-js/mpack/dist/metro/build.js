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
var build_exports = {};
__export(build_exports, {
  build: () => build,
  buildAll: () => buildAll
});
module.exports = __toCommonJS(build_exports);
var import_path = __toESM(require("path"));
var import_plugin_core = require("@granite-js/plugin-core");
var import_es_toolkit = require("es-toolkit");
var import_getMetroConfig = require("./getMetroConfig");
var import_getDefaultOutfileName = require("../utils/getDefaultOutfileName");
var import_src = __toESM(require("../vendors/metro/src"));
async function build({ config, ...options }) {
  const driver = (0, import_plugin_core.createPluginHooksDriver)(config);
  await driver.build.pre();
  const buildResult = await buildImpl(config, options);
  await driver.build.post({
    buildResults: [buildResult]
  });
  return buildResult;
}
async function buildAll(optionsList, { config, concurrency = 2 }) {
  const buildResults = [];
  const semaphore = new import_es_toolkit.Semaphore(Math.min(concurrency, optionsList.length));
  const driver = (0, import_plugin_core.createPluginHooksDriver)(config);
  await driver.build.pre();
  await Promise.all(
    optionsList.map(async (options) => {
      await semaphore.acquire();
      try {
        const buildResult = await buildImpl(config, options);
        buildResults.push(buildResult);
      } finally {
        semaphore.release();
      }
    })
  );
  await driver.build.post({ buildResults });
  return buildResults;
}
async function buildImpl(config, { platform, outfile, minify = false, dev = true }) {
  const resolvedConfig = await (0, import_plugin_core.resolveConfig)(config);
  const metroConfig = await (0, import_getMetroConfig.getMetroConfig)({ rootPath: config.cwd }, resolvedConfig?.metro ?? {});
  const outfileName = outfile == null ? (0, import_getDefaultOutfileName.getDefaultOutfileName)(config.entryFile, platform) : outfile;
  const outfilePath = import_path.default.join(config.outdir, outfileName);
  await import_src.default.runBuild(metroConfig, {
    platform,
    entry: config.entryFile,
    out: outfilePath,
    minify,
    dev
  });
  return buildResultShim(config, { outfile: outfilePath, platform, minify, dev });
}
function buildResultShim(config, options) {
  const unsupportedField = new Proxy({}, {
    get: (_, key) => {
      throw new Error(`Unsupported field: ${key.toString()}`);
    }
  });
  return {
    platform: options.platform,
    bundle: unsupportedField,
    outfile: import_path.default.join(config.outdir, options.outfile),
    outputFiles: [{ path: options.outfile, contents: new Uint8Array(), hash: "", text: "" }],
    sourcemapOutfile: "",
    warnings: [],
    errors: [],
    extra: unsupportedField,
    mangleCache: unsupportedField,
    metafile: unsupportedField,
    size: 0,
    duration: 0,
    totalModuleCount: 0
  };
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  build,
  buildAll
});
