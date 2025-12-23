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
var import_CountingSet = __toESM(require("./CountingSet"));
const countLines = require("./countLines");
const getPreludeCode = require("./getPreludeCode");
const transformHelpers = require("./transformHelpers");
const defaults = require("../../../metro-config/src/defaults/defaults");
async function getPrependedScripts(config, options, resolverOptions, bundler, deltaBundler) {
  const polyfillModuleNames = config.serializer.getPolyfills({
    platform: options.platform
  }).concat(config.serializer.polyfillModuleNames);
  const transformOptions = {
    ...options,
    type: "script"
  };
  const dependencies = await deltaBundler.getDependencies([defaults.moduleSystem, ...polyfillModuleNames], {
    resolve: await transformHelpers.getResolveDependencyFn(bundler, options.platform, resolverOptions),
    transform: await transformHelpers.getTransformFn(
      [defaults.moduleSystem, ...polyfillModuleNames],
      bundler,
      deltaBundler,
      config,
      transformOptions,
      resolverOptions
    ),
    unstable_allowRequireContext: config.transformer.unstable_allowRequireContext,
    transformOptions,
    onProgress: null,
    experimentalImportBundleSupport: config.transformer.experimentalImportBundleSupport,
    shallow: false
  });
  return [
    _getPrelude({
      dev: options.dev,
      globalPrefix: config.transformer.globalPrefix,
      requireCycleIgnorePatterns: config.resolver.requireCycleIgnorePatterns
    }),
    ...dependencies.values()
  ];
}
function _getPrelude({ dev, globalPrefix, requireCycleIgnorePatterns }) {
  const { compile } = require("metro-hermes-compiler");
  const code = getPreludeCode({
    isDev: dev,
    globalPrefix,
    requireCycleIgnorePatterns
  });
  const name = "__prelude__";
  return {
    dependencies: /* @__PURE__ */ new Map(),
    getSource: () => Buffer.from(code),
    inverseDependencies: new import_CountingSet.default(),
    path: name,
    output: [
      {
        type: "js/script/virtual",
        data: {
          code,
          lineCount: countLines(code),
          map: []
        }
      },
      {
        type: "bytecode/script/virtual",
        data: {
          bytecode: compile(code, { sourceURL: "__prelude__.virtual.js" }).bytecode
        }
      }
    ]
  };
}
module.exports = getPrependedScripts;
