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
var getMetroConfig_exports = {};
__export(getMetroConfig_exports, {
  getMetroConfig: () => getMetroConfig
});
module.exports = __toCommonJS(getMetroConfig_exports);
var import_path = __toESM(require("path"));
var import_utils = require("@granite-js/utils");
var import_enhancedResolver = require("./enhancedResolver");
var import_getMonorepoRoot = require("./getMonorepoRoot");
var import_constants = require("../constants");
var import_defaults = require("../vendors/metro-config/src/defaults");
var import_exclusionList = __toESM(require("../vendors/metro-config/src/defaults/exclusionList"));
var import_loadConfig = require("../vendors/metro-config/src/loadConfig");
const INTERNAL_CALLSITES_REGEX = new RegExp(
  [
    "/Libraries/Renderer/implementations/.+\\.js$",
    "/Libraries/BatchedBridge/MessageQueue\\.js$",
    "/Libraries/YellowBox/.+\\.js$",
    "/Libraries/LogBox/.+\\.js$",
    "/Libraries/Core/Timers/.+\\.js$",
    "/Libraries/WebSocket/.+\\.js$",
    "/Libraries/vendor/.+\\.js$",
    "/node_modules/react-devtools-core/.+\\.js$",
    "/node_modules/react-refresh/.+\\.js$",
    "/node_modules/scheduler/.+\\.js$",
    "/node_modules/event-target-shim/.+\\.js$",
    "/node_modules/invariant/.+\\.js$",
    "/node_modules/react-native/index.js$",
    "/metro-runtime/.+\\.js$",
    "^\\[native code\\]$"
  ].join("|")
);
async function getMetroConfig({ rootPath }, additionalConfig) {
  const defaultConfig = (0, import_defaults.getDefaultValues)(rootPath);
  const reactNativePath = import_path.default.dirname(resolveFromRoot("react-native/package.json", rootPath));
  const resolvedRootPath = await (0, import_getMonorepoRoot.getMonorepoRoot)(rootPath);
  const packageRootPath = await (0, import_utils.getPackageRoot)();
  const resolveRequest = additionalConfig?.resolver?.resolveRequest ?? (0, import_enhancedResolver.createResolver)(rootPath, {
    conditionNames: additionalConfig?.resolver?.conditionNames
  });
  return (0, import_loadConfig.mergeConfig)(defaultConfig, {
    projectRoot: additionalConfig?.projectRoot || rootPath,
    watchFolders: [resolvedRootPath, packageRootPath, ...additionalConfig?.watchFolders || []],
    transformerPath: resolveVendors("metro-transform-worker/src"),
    transformer: {
      allowOptionalDependencies: true,
      assetRegistryPath: import_path.default.resolve(__dirname, "assetRegistry.js"),
      workerPath: resolveVendors("metro/src/DeltaBundler/Worker"),
      getTransformOptions: async () => ({
        transform: {
          experimentalImportSupport: false,
          inlineRequires: false
        }
      }),
      babelTransformerPath: require.resolve("metro-react-native-babel-transformer"),
      asyncRequireModulePath: require.resolve("metro-runtime/src/modules/asyncRequire"),
      unstable_collectDependenciesPath: resolveVendors("metro/src/ModuleGraph/worker/collectDependencies"),
      unstable_allowRequireContext: true,
      /**
       * @see [WorkerFarm.js](../vendors/metro/src/DeltaBundler/WorkerFarm.js)
       */
      INTERNAL__transformSync: additionalConfig?.transformSync,
      /**
       * @see [index.js](../vendors/metro-transform-worker/src/index.js)
       */
      INTERNAL__babelConfig: additionalConfig?.babelConfig
    },
    resolver: {
      // metro
      platforms: ["android", "ios"],
      useWatchman: false,
      resolveRequest,
      // metro-file-map
      sourceExts: [...import_constants.SOURCE_EXTENSIONS.map((extension) => extension.replace(/^\.?/, "")), "cjs", "mjs"],
      blockList: (0, import_exclusionList.default)(
        additionalConfig?.resolver?.blockList ? asArray(additionalConfig.resolver.blockList) : []
      ),
      nodeModulesPaths: additionalConfig?.resolver?.nodeModulesPaths || [],
      extraNodeModules: additionalConfig?.resolver?.extraNodeModules || {},
      disableHierarchicalLookup: additionalConfig?.resolver?.disableHierarchicalLookup,
      resolverMainFields: additionalConfig?.resolver?.resolverMainFields ?? import_constants.RESOLVER_MAIN_FIELDS
    },
    serializer: {
      getModulesRunBeforeMainModule: () => [resolveFromRoot("react-native/Libraries/Core/InitializeCore", rootPath)],
      getPolyfills: () => [
        ...require(import_path.default.join(reactNativePath, "rn-get-polyfills"))(),
        ...additionalConfig?.serializer?.getPolyfills?.() ?? []
      ]
    },
    symbolicator: {
      customizeFrame: (frame) => {
        const collapse = Boolean(frame.file && INTERNAL_CALLSITES_REGEX.test(frame.file));
        return { collapse };
      }
    },
    server: {
      port: import_constants.DEV_SERVER_DEFAULT_PORT
    },
    reporter: additionalConfig?.reporter,
    ...process.env.METRO_RESET_CACHE !== "false" ? { resetCache: true } : {}
  });
}
function resolveFromRoot(source, rootPath) {
  return require.resolve(source, { paths: [rootPath] });
}
function resolveVendors(source) {
  const vendorsBase = import_path.default.resolve(__dirname, "../vendors");
  return require.resolve(import_path.default.join(vendorsBase, source));
}
function asArray(value) {
  return Array.isArray(value) ? value : [value];
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  getMetroConfig
});
