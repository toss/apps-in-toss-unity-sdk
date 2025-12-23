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
var import_metro_file_map = __toESM(require("metro-file-map"));
const ci = require("ci-info");
const path = require("path");
function getIgnorePattern(config) {
  const { blockList, blacklistRE } = config.resolver;
  const ignorePattern = blacklistRE || blockList;
  if (!ignorePattern) {
    return / ^/;
  }
  const combine = (regexes) => new RegExp(regexes.map((regex) => "(" + regex.source.replace(/\//g, path.sep) + ")").join("|"));
  if (Array.isArray(ignorePattern)) {
    return combine(ignorePattern);
  }
  return ignorePattern;
}
function createHasteMap(config, options) {
  const dependencyExtractor = options?.extractDependencies === false ? null : config.resolver.dependencyExtractor;
  const computeDependencies = dependencyExtractor != null;
  return import_metro_file_map.default.create({
    cacheManagerFactory: config?.unstable_fileMapCacheManagerFactory ?? ((buildParameters) => new import_metro_file_map.DiskCacheManager({
      buildParameters,
      cacheDirectory: config.fileMapCacheDirectory ?? config.hasteMapCacheDirectory,
      cacheFilePrefix: options?.cacheFilePrefix
    })),
    perfLogger: config.unstable_perfLogger?.subSpan("hasteMap") ?? null,
    computeDependencies,
    computeSha1: true,
    dependencyExtractor: config.resolver.dependencyExtractor,
    extensions: Array.from(
      /* @__PURE__ */ new Set([...config.resolver.sourceExts, ...config.resolver.assetExts, ...config.watcher.additionalExts])
    ),
    forceNodeFilesystemAPI: !config.resolver.useWatchman,
    hasteImplModulePath: config.resolver.hasteImplModulePath,
    ignorePattern: getIgnorePattern(config),
    maxWorkers: config.maxWorkers,
    mocksPattern: "",
    platforms: config.resolver.platforms,
    retainAllFiles: true,
    resetCache: config.resetCache,
    rootDir: config.projectRoot,
    roots: config.watchFolders,
    throwOnModuleCollision: options?.throwOnModuleCollision ?? true,
    useWatchman: config.resolver.useWatchman,
    watch: options?.watch == null ? !ci.isCI : options.watch,
    watchmanDeferStates: config.watcher.watchman.deferStates
  });
}
module.exports = createHasteMap;
