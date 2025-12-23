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
var fs = __toESM(require("fs/promises"));
var path = __toESM(require("path"));
var import_plugin_core = require("@granite-js/plugin-core");
var import_es_toolkit = require("es-toolkit");
var import_bundler = require("../bundler");
var import_performance = require("../performance");
var import_buildResult = require("../utils/buildResult");
var import_getDefaultOutfileName = require("../utils/getDefaultOutfileName");
var import_printErrors = require("../utils/printErrors");
var import_promise = require("../utils/promise");
var import_writeBundle = require("../utils/writeBundle");
async function build({ config, plugins = [], ...options }) {
  const driver = (0, import_plugin_core.createPluginHooksDriver)(config);
  await driver.build.pre();
  const buildResult = await buildImpl(config, plugins, options);
  await driver.build.post({ buildResults: [buildResult] });
  return buildResult;
}
async function buildAll(optionsList, { config, plugins = [], concurrency = 2 }) {
  const semaphore = new import_es_toolkit.Semaphore(Math.min(concurrency, optionsList.length));
  const driver = (0, import_plugin_core.createPluginHooksDriver)(config);
  await driver.build.pre();
  const taskResults = await Promise.allSettled(
    optionsList.map(async (options) => {
      await semaphore.acquire();
      try {
        const buildResult = await buildImpl(config, plugins, options);
        return buildResult;
      } finally {
        semaphore.release();
      }
    })
  );
  const errors = taskResults.filter(import_promise.isRejected);
  if (errors.length) {
    (0, import_printErrors.printErrors)(errors);
    throw new Error("Build failed");
  }
  const buildResults = taskResults.filter(import_promise.isFulfilled).map((result) => result.value);
  await driver.build.post({ buildResults });
  return buildResults;
}
async function buildImpl(config, plugins, { platform, outfile, cache = true, dev = true, metafile = false }) {
  const { metro: _metroConfig, devServer: _devServerConfig, ...buildConfig } = await (0, import_plugin_core.resolveConfig)(config);
  const outfileName = outfile == null ? (0, import_getDefaultOutfileName.getDefaultOutfileName)(config.entryFile, platform) : outfile;
  const outfilePath = path.resolve(config.outdir, outfileName);
  const bundler = new import_bundler.Bundler({
    rootDir: config.cwd,
    cache,
    dev,
    metafile,
    buildConfig: {
      platform,
      entry: config.entryFile,
      outfile: outfilePath,
      ...buildConfig
    }
  });
  for (const plugin of plugins) {
    bundler.addPlugin(plugin());
  }
  const buildResult = await bundler.build();
  if ((0, import_buildResult.isBuildSuccess)(buildResult)) {
    await (0, import_writeBundle.writeBundle)(buildResult.outfile, buildResult.bundle);
    if (buildResult.metafile != null) {
      await (0, import_writeBundle.writeMetafile)(buildResult.outfile, buildResult.metafile);
    }
    const performanceSummary = import_performance.Performance.getSummary();
    if (performanceSummary != null) {
      (0, import_performance.printSummary)(performanceSummary);
      await fs.writeFile(path.join(config.cwd, "perf.json"), JSON.stringify(performanceSummary, null, 2), "utf-8");
    }
    return buildResult;
  } else {
    throw new Error("Build failed");
  }
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  build,
  buildAll
});
