"use strict";
var import_metro_file_map = require("metro-file-map");
const { ModuleResolver } = require("../../node-haste/DependencyGraph/ModuleResolution");
const parsePlatformFilePath = require("../../node-haste/lib/parsePlatformFilePath");
const HasteFS = require("./HasteFS");
const Module = require("./Module");
const ModuleCache = require("./ModuleCache");
const defaults = require("../../../../metro-config/src/defaults/defaults");
const path = require("path");
const NATIVE_PLATFORM = "native";
const GENERIC_PLATFORM = "g";
const PACKAGE_JSON = path.sep + "package.json";
const NULL_MODULE = {
  path: "/",
  getPackage() {
  },
  isHaste() {
    throw new Error("not implemented");
  },
  getName() {
    throw new Error("not implemented");
  }
};
const NODE_MODULES = path.sep + "node_modules" + path.sep;
const isNodeModules = (file) => file.includes(NODE_MODULES);
const createModuleMap = ({ files, moduleCache, sourceExts, additionalExts, platforms }) => {
  const platformSet = new Set((platforms ?? defaults.platforms).concat([NATIVE_PLATFORM]));
  const map = /* @__PURE__ */ new Map();
  files.forEach((filePath) => {
    if (isNodeModules(filePath)) {
      return;
    }
    let id;
    let module2;
    const fileExt = path.extname(filePath).substr(1);
    if (filePath.endsWith(PACKAGE_JSON)) {
      module2 = moduleCache.getPackage(filePath);
      id = module2.data.name;
    } else if (sourceExts.has(fileExt) || additionalExts.has(fileExt)) {
      module2 = moduleCache.getModule(filePath);
      id = module2.name;
    }
    if (!(id && module2 && module2.isHaste())) {
      return;
    }
    const mapModule = map.get(id) || /* @__PURE__ */ Object.create(null);
    const platform = parsePlatformFilePath(filePath, platformSet).platform || GENERIC_PLATFORM;
    const existingModule = mapModule[platform];
    mapModule[platform] = [filePath, module2.type === "Package" ? 1 : 0];
    if (existingModule && existingModule[0] !== filePath) {
      throw new Error(
        [
          "@providesModule naming collision:",
          `  Duplicate module name: \`${id}\``,
          `  Paths: \`${filePath}\` collides with \`${existingModule[0]}\``,
          "",
          "This error is caused by a @providesModule declaration with the same name across two different files."
        ].join("\n")
      );
    }
    map.set(id, mapModule);
  });
  return map;
};
exports.createResolveFn = function(options) {
  const {
    assetExts,
    assetResolutions,
    extraNodeModules,
    transformedFiles,
    sourceExts,
    additionalExts,
    platform,
    platforms
  } = options;
  const files = Object.keys(transformedFiles);
  function getTransformedFile(path2) {
    const result = transformedFiles[path2];
    if (!result) {
      throw new Error(`"${path2} does not exist`);
    }
    return result;
  }
  const hasteFS = new HasteFS(files);
  const moduleCache = new ModuleCache((filePath) => hasteFS.closest(filePath, "package.json"), getTransformedFile);
  const assetExtensions = new Set(assetExts.map((asset) => "." + asset));
  const isAssetFile = (file) => assetExtensions.has(path.extname(file));
  const moduleResolver = new ModuleResolver({
    dirExists: (filePath) => hasteFS.dirExists(filePath),
    disableHierarchicalLookup: options.disableHierarchicalLookup,
    doesFileExist: (filePath) => hasteFS.exists(filePath),
    emptyModulePath: options.emptyModulePath,
    extraNodeModules,
    isAssetFile,
    mainFields: options.mainFields,
    moduleCache,
    moduleMap: new import_metro_file_map.ModuleMap({
      duplicates: /* @__PURE__ */ new Map(),
      map: createModuleMap({
        files,
        moduleCache,
        sourceExts: new Set(sourceExts),
        additionalExts: new Set(additionalExts),
        platforms
      }),
      mocks: /* @__PURE__ */ new Map(),
      rootDir: ""
    }),
    nodeModulesPaths: options.nodeModulesPaths,
    preferNativePlatform: true,
    projectRoot: "",
    resolveAsset: (dirPath, assetName, extension) => {
      const basePath = dirPath + path.sep + assetName;
      const assets = [
        basePath + extension,
        ...assetResolutions.map((resolution) => basePath + "@" + resolution + "x" + extension)
      ].filter((candidate) => hasteFS.exists(candidate));
      return assets.length ? assets : null;
    },
    resolveRequest: options.resolveRequest,
    sourceExts
  });
  return (id, sourcePath) => {
    const from = sourcePath != null ? new Module(sourcePath, moduleCache, getTransformedFile(sourcePath)) : NULL_MODULE;
    return moduleResolver.resolveDependency(from, id, true, platform, options.resolverOptions).path;
  };
};
