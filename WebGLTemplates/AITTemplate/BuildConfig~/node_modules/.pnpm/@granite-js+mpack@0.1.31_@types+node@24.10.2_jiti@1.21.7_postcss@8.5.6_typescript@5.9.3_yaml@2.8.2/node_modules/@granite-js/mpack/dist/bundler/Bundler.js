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
var Bundler_exports = {};
__export(Bundler_exports, {
  Bundler: () => Bundler
});
module.exports = __toCommonJS(Bundler_exports);
var path = __toESM(require("path"));
var import_es_toolkit = require("es-toolkit");
var esbuild = __toESM(require("esbuild"));
var import_PluginDriver = require("./PluginDriver");
var import_plugins = require("./plugins");
var import_constants = require("../constants");
var import_logger = require("../logger");
var import_getId = require("../utils/getId");
var import_promise = require("../utils/promise");
var import_presets = require("./internal/presets");
var import_progressBar = require("../utils/progressBar");
class Bundler {
  constructor(config) {
    this.config = config;
    this.setupUncaughtExceptionHandler();
    const id = (0, import_getId.getId)(config);
    this.id = id;
    this.pluginDriver = new import_PluginDriver.PluginDriver(id);
    this.config.buildConfig.entry = path.resolve(this.config.rootDir, this.config.buildConfig.entry);
    this.config.buildConfig.outfile = path.resolve(this.config.rootDir, this.config.buildConfig.outfile);
    this.config.buildConfig = (0, import_presets.combineWithBaseBuildConfig)(this.config, {
      rootDir: this.config.rootDir,
      dev: this.config.dev
    });
    import_logger.logger.debug("Bundler.constructor", { id, config });
  }
  id;
  revisionId = 0;
  status = "idle";
  pluginDriver;
  esbuildContext = null;
  bundleTask = null;
  async build(options) {
    const { withDispose = true } = options ?? {};
    if (this.esbuildContext == null) {
      this.esbuildContext = await esbuild.context(this.getBaseBuildOptions());
    }
    if (this.status === "prepared" || this.status === "building") {
      this.bundleTask?.abort();
    }
    this.bundleTask = new import_promise.PromiseHandler(this.revisionId++);
    this.esbuildContext.rebuild();
    this.status = "prepared";
    const result = await this.bundleTask.wait();
    await (withDispose && this.esbuildContext.dispose());
    return result;
  }
  getId() {
    return this.id;
  }
  addPlugin(plugin) {
    this.pluginDriver.addPlugin(plugin);
    return this;
  }
  setupUncaughtExceptionHandler() {
    if (process.hasUncaughtExceptionCaptureCallback()) {
      return;
    }
    process.setUncaughtExceptionCaptureCallback((error) => {
      (0, import_progressBar.cleanup)();
      import_logger.logger.error(`Uncaught exception occurred

${error.stack ?? error.message}`);
      process.exit(1);
    });
  }
  getBaseBuildOptions() {
    const { rootDir, metafile, buildConfig } = this.config;
    const { platform, entry, outfile = "bundle.js", esbuild: esbuild2 = {} } = buildConfig;
    const { prelude: _, ...esbuildOptions } = esbuild2;
    const platforms = [platform, "native", "react-native"];
    const supportedExtensions = [...import_constants.SOURCE_EXTENSIONS, ...import_constants.ASSET_EXTENSIONS];
    const pluginContext = { id: this.id, config: this.config };
    const resolveExtensions = [
      ...platforms.map((platform2) => supportedExtensions.map((ext) => `.${platform2}${ext}`)),
      ...supportedExtensions
    ].flat();
    this.setupEnvironment();
    return {
      entryPoints: [path.resolve(rootDir, entry)],
      outfile: path.resolve(rootDir, outfile),
      sourcemap: true,
      sourcesContent: true,
      bundle: true,
      resolveExtensions,
      mainFields: import_constants.RESOLVER_MAIN_FIELDS,
      conditions: import_constants.RESOLVER_EXPORTS_MAP_CONDITIONS,
      target: "hermes0.11",
      format: "iife",
      loader: {
        /**
         * 토스에서는 에셋을 로컬에서 로드하지 않고 Remote 에서
         * 로드하기 때문에 empty loader 를 사용하여 에셋을 번들링 결과에서 제외
         *
         * @see docs {@link https://esbuild.github.io/content-types/#empty-file}
         */
        ...Object.fromEntries(import_constants.ASSET_EXTENSIONS.map((ext) => [ext, "empty"]))
      },
      supported: {
        "const-and-let": false
      },
      legalComments: "none",
      jsx: "automatic",
      logLevel: "silent",
      ...esbuildOptions,
      /**
       * 번들 결과를 파일 시스템에 저장하지 않고 메모리 내에 들고 처리하기 위해 false 로 지정
       *
       * - 빌드: 메모리의 있는 번들 직접 File System에 쓰기
       * - 개발 서버: 메모리에서 번들 로드
       *
       * @see {@link https://esbuild.github.io/api/#write}
       * @see {handleEnd}
       */
      write: false,
      metafile,
      plugins: [
        (0, import_plugins.buildStatusPlugin)({
          context: pluginContext,
          onPrepare: () => this.handlePrepare(),
          onStart: () => this.handleStart(),
          onLoad: (moduleCount) => this.handleLoad(moduleCount),
          onEnd: (buildResult) => this.handleEnd(buildResult)
        }),
        (0, import_plugins.resolvePlugin)({ context: pluginContext }),
        (0, import_plugins.requireContextPlugin)(),
        (0, import_plugins.transformPlugin)({
          context: pluginContext,
          transformSync: buildConfig.transformer?.transformSync,
          transformAsync: buildConfig.transformer?.transformAsync
        }),
        ...esbuildOptions?.plugins ?? []
      ].filter(import_es_toolkit.isNotNil)
    };
  }
  setupEnvironment() {
    const envString = this.config.dev ?? true ? "development" : "production";
    process.env.NODE_ENV = envString;
    process.env.BABEL_ENV = envString;
  }
  handlePrepare() {
    this.status = "prepared";
    this.pluginDriver.hookSync("prepare", [this.config]);
  }
  handleStart() {
    this.status = "building";
    this.pluginDriver.hookSync("buildStart", []);
  }
  handleLoad({ moduleCount }) {
    this.pluginDriver.hookSync("load", [moduleCount]);
  }
  handleEnd(buildResult) {
    this.status = "idle";
    this.bundleTask?.done(buildResult);
    this.pluginDriver.hookSync("buildEnd", [buildResult]);
  }
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  Bundler
});
