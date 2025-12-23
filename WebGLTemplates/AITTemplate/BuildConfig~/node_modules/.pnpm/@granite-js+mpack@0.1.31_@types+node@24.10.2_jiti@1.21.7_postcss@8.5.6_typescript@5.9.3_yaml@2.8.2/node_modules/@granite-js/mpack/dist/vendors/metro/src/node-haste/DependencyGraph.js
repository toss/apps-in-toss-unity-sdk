"use strict";
var import_metro_file_map = require("metro-file-map");
const canonicalize = require("../../../metro-core/src/canonicalize");
const createHasteMap = require("./DependencyGraph/createHasteMap");
const { ModuleResolver } = require("./DependencyGraph/ModuleResolution");
const ModuleCache = require("./ModuleCache");
const { EventEmitter } = require("events");
const fs = require("fs");
const {
  AmbiguousModuleResolutionError,
  Logger: { createActionStartEntry, createActionEndEntry, log },
  PackageResolutionError
} = require("../../../metro-core/src");
const { InvalidPackageError } = require("metro-resolver");
const nullthrows = require("nullthrows");
const path = require("path");
const isPnP = require("../isPnP");
let pnpapi;
if (isPnP()) {
  pnpapi = require("pnpapi");
}
const { DuplicateHasteCandidatesError } = import_metro_file_map.ModuleMap;
const NULL_PLATFORM = Symbol();
function getOrCreateMap(map, field) {
  let subMap = map.get(field);
  if (!subMap) {
    subMap = /* @__PURE__ */ new Map();
    map.set(field, subMap);
  }
  return subMap;
}
class DependencyGraph extends EventEmitter {
  _assetExtensions;
  _config;
  _haste;
  _hasteFS;
  _moduleCache;
  _moduleMap;
  _moduleResolver;
  _resolutionCache;
  _readyPromise;
  constructor(config, options) {
    super();
    this._config = config;
    this._assetExtensions = new Set(config.resolver.assetExts.map((asset) => "." + asset));
    const { hasReducedPerformance, watch } = options ?? {};
    const initializingMetroLogEntry = log(createActionStartEntry("Initializing Metro"));
    config.reporter.update({
      type: "dep_graph_loading",
      hasReducedPerformance: !!hasReducedPerformance
    });
    const haste = createHasteMap(config, { watch });
    haste.setMaxListeners(1e3);
    this._haste = haste;
    this._readyPromise = haste.build().then(({ hasteFS, moduleMap }) => {
      log(createActionEndEntry(initializingMetroLogEntry));
      config.reporter.update({ type: "dep_graph_loaded" });
      this._hasteFS = hasteFS;
      this._moduleMap = moduleMap;
      this._haste.on("change", this._onHasteChange.bind(this));
      this._resolutionCache = /* @__PURE__ */ new Map();
      this._moduleCache = this._createModuleCache();
      this._createModuleResolver();
    });
  }
  // Waits for the dependency graph to become ready after initialisation.
  // Don't read anything from the graph until this resolves.
  async ready() {
    await this._readyPromise;
  }
  // Creates the dependency graph and waits for it to become ready.
  // @deprecated Use the constructor + ready() directly.
  static async load(config, options) {
    const self = new DependencyGraph(config, options);
    await self.ready();
    return self;
  }
  _getClosestPackage(filePath) {
    const parsedPath = path.parse(filePath);
    const root = parsedPath.root;
    let dir = parsedPath.dir;
    do {
      const candidate = path.join(dir, "package.json");
      if (this._hasteFS.exists(candidate)) {
        return candidate;
      }
      dir = path.dirname(dir);
    } while (dir !== "." && dir !== root);
    return null;
  }
  /* $FlowFixMe[missing-local-annot] The type annotation(s) required by Flow's
   * LTI update could not be added via codemod */
  _onHasteChange({ eventsQueue, hasteFS, moduleMap }) {
    this._hasteFS = hasteFS;
    this._resolutionCache = /* @__PURE__ */ new Map();
    this._moduleMap = moduleMap;
    eventsQueue.forEach(({ type, filePath }) => this._moduleCache.processFileChange(type, filePath));
    this._createModuleResolver();
    this.emit("change");
  }
  _createModuleResolver() {
    this._moduleResolver = new ModuleResolver({
      dirExists: (filePath) => {
        try {
          return fs.lstatSync(filePath).isDirectory();
        } catch (e) {
        }
        return false;
      },
      disableHierarchicalLookup: this._config.resolver.disableHierarchicalLookup,
      doesFileExist: this._doesFileExist,
      emptyModulePath: this._config.resolver.emptyModulePath,
      extraNodeModules: this._config.resolver.extraNodeModules,
      isAssetFile: (file) => this._assetExtensions.has(path.extname(file)),
      mainFields: this._config.resolver.resolverMainFields,
      moduleCache: this._moduleCache,
      moduleMap: this._moduleMap,
      nodeModulesPaths: this._config.resolver.nodeModulesPaths,
      preferNativePlatform: true,
      projectRoot: this._config.projectRoot,
      resolveAsset: (dirPath, assetName, extension) => {
        const basePath = dirPath + path.sep + assetName;
        const assets = [
          basePath + extension,
          ...this._config.resolver.assetResolutions.map((resolution) => basePath + "@" + resolution + "x" + extension)
        ].filter((candidate) => this._hasteFS.exists(candidate));
        return assets.length ? assets : null;
      },
      resolveRequest: this._config.resolver.resolveRequest,
      sourceExts: this._config.resolver.sourceExts
    });
  }
  _createModuleCache() {
    return new ModuleCache({
      // $FlowFixMe[method-unbinding] added when improving typing for this parameters
      getClosestPackage: this._getClosestPackage.bind(this)
    });
  }
  getSha1(filename) {
    const splitIndex = filename.indexOf(".zip/");
    const containerName = splitIndex !== -1 ? filename.slice(0, splitIndex + 4) : filename;
    const realpath = fs.realpathSync(containerName);
    const resolvedPath = (pnpapi ? pnpapi.resolveVirtual(realpath) : realpath) ?? realpath;
    const sha1 = this._hasteFS.getSha1(resolvedPath);
    if (!sha1) {
      throw new ReferenceError(
        `SHA-1 for file ${filename} (${resolvedPath}) is not computed.
         Potential causes:
           1) You have symlinks in your project - watchman does not follow symlinks.
           2) Check \`blockList\` in your metro.config.js and make sure it isn't excluding the file path.`
      );
    }
    return sha1;
  }
  getWatcher() {
    return this._haste;
  }
  end() {
    this._haste.end();
  }
  /** Given a search context, return a list of file paths matching the query. */
  matchFilesWithContext(from, context) {
    return this._hasteFS.matchFilesWithContext(from, context);
  }
  resolveDependency(from, to, platform, resolverOptions, { assumeFlatNodeModules } = {
    assumeFlatNodeModules: false
  }) {
    const isSensitiveToOriginFolder = (
      // Resolution is always relative to the origin folder unless we assume a flat node_modules
      !assumeFlatNodeModules || // Path requests are resolved relative to the origin folder
      to.includes("/") || to === "." || to === ".." || // Preserve standard assumptions under node_modules
      from.includes(path.sep + "node_modules" + path.sep)
    );
    const resolverOptionsKey = JSON.stringify(resolverOptions.customResolverOptions ?? {}, canonicalize) ?? "";
    const originKey = isSensitiveToOriginFolder ? path.dirname(from) : "";
    const targetKey = to;
    const platformKey = platform ?? NULL_PLATFORM;
    const mapByResolverOptions = this._resolutionCache;
    const mapByOrigin = getOrCreateMap(mapByResolverOptions, resolverOptionsKey);
    const mapByTarget = getOrCreateMap(mapByOrigin, originKey);
    const mapByPlatform = getOrCreateMap(mapByTarget, targetKey);
    let modulePath = mapByPlatform.get(platformKey);
    if (!modulePath) {
      try {
        modulePath = this._moduleResolver.resolveDependency(
          this._moduleCache.getModule(from),
          to,
          true,
          platform,
          resolverOptions
        ).path;
      } catch (error) {
        if (error instanceof DuplicateHasteCandidatesError) {
          throw new AmbiguousModuleResolutionError(from, error);
        }
        if (error instanceof InvalidPackageError) {
          throw new PackageResolutionError({
            packageError: error,
            originModulePath: from,
            targetModuleName: to
          });
        }
        throw error;
      }
    }
    mapByPlatform.set(platformKey, modulePath);
    return modulePath;
  }
  _doesFileExist = (filePath) => {
    return this._hasteFS.exists(filePath);
  };
  getHasteName(filePath) {
    const hasteName = this._hasteFS.getModuleName(filePath);
    if (hasteName) {
      return hasteName;
    }
    return path.relative(this._config.projectRoot, filePath);
  }
  getDependencies(filePath) {
    return nullthrows(this._hasteFS.getDependencies(filePath));
  }
}
module.exports = DependencyGraph;
