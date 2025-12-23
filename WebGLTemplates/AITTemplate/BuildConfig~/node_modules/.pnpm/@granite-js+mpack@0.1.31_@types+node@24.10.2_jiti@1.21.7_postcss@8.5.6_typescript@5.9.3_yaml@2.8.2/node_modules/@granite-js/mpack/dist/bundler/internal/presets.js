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
var presets_exports = {};
__export(presets_exports, {
  combineWithBaseBuildConfig: () => combineWithBaseBuildConfig,
  defineGlobalVariables: () => defineGlobalVariables,
  getReactNativeSetupScripts: () => getReactNativeSetupScripts,
  globalVariables: () => globalVariables
});
module.exports = __toCommonJS(presets_exports);
var import_path = __toESM(require("path"));
var import_plugin_core = require("@granite-js/plugin-core");
function getReactNativeSetupScripts({ rootDir }) {
  const reactNativePath = import_path.default.dirname(
    require.resolve("react-native/package.json", {
      paths: [rootDir]
    })
  );
  return [
    ...require(import_path.default.join(reactNativePath, "rn-get-polyfills"))(),
    import_path.default.join(reactNativePath, "Libraries/Core/InitializeCore.js")
  ];
}
function globalVariables({ dev }) {
  return [
    "var __BUNDLE_START_TIME__=this.nativePerformanceNow?nativePerformanceNow():Date.now();",
    `var __DEV__=${JSON.stringify(dev)};`,
    `var global=typeof globalThis!=='undefined'?globalThis:typeof global!=='undefined'?global:typeof window!=='undefined'?window:this;`
  ].join("\n");
}
function defineGlobalVariables({ dev }) {
  return {
    window: "global",
    __DEV__: JSON.stringify(dev),
    "process.env.NODE_ENV": JSON.stringify(dev ? "development" : "production")
  };
}
function combineWithBaseBuildConfig(config, context) {
  return (0, import_plugin_core.mergeBuildConfigs)(
    {
      entry: config.buildConfig.entry,
      outfile: config.buildConfig.outfile,
      platform: config.buildConfig.platform,
      esbuild: {
        define: defineGlobalVariables({ dev: context.dev }),
        prelude: getReactNativeSetupScripts({ rootDir: context.rootDir }),
        banner: {
          js: [
            globalVariables({ dev: context.dev }),
            /**
             * Polyfill for `@swc/helpers` build compatibility
             *
             * @see https://github.com/swc-project/swc/blob/v1.4.15/packages/helpers/esm/_async_iterator.js#L3
             *
             * - babel: No runtime issues after build as there is a fallback for `Symbol.asyncIterator`
             * - swc: No fallback for `Symbol.asyncIterator`, so it needs to be defined in advance
             */
            `(function(){if(typeof Symbol!=="undefined"&&!Symbol.asyncIterator){Symbol.asyncIterator=Symbol.for("@@asyncIterator")}})();`
          ].join("\n")
        }
      },
      babel: {
        conditions: [
          /**
           * @TODO
           * We're using a RegExp in Zod that's not supported by Hermes,
           * so we're switching to Babel for transpilation since there's no compatible SWC config or plugin available.
           *
           * @see zod {@link https://github.com/colinhacks/zod/issues/2302}
           */
          (_code, path2) => path2.includes("node_modules/zod")
        ]
      }
    },
    config.buildConfig
  );
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  combineWithBaseBuildConfig,
  defineGlobalVariables,
  getReactNativeSetupScripts,
  globalVariables
});
