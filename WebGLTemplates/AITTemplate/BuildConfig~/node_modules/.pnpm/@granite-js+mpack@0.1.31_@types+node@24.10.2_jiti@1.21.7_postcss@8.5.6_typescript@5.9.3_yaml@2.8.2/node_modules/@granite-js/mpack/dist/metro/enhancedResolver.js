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
var enhancedResolver_exports = {};
__export(enhancedResolver_exports, {
  createResolver: () => createResolver
});
module.exports = __toCommonJS(enhancedResolver_exports);
var import_module = __toESM(require("module"));
var import_path = __toESM(require("path"));
var import_enhanced_resolve = __toESM(require("enhanced-resolve"));
var import_constants = require("../constants");
const SINGLETON_MODULES = ["@babel/runtime"];
const NATIVE_MODULES = ["react-native", "react"];
const SUPPORTED_BUILTIN_FALLBACKS = {
  http: "stream-http",
  https: "https-browserify",
  zlib: "browserify-zlib",
  url: "url",
  util: "util",
  path: "path-browserify",
  buffer: "buffer",
  stream: "stream-browserify",
  assert: "assert",
  events: "events"
};
const builtinModules = new Set(import_module.default.builtinModules);
const resolvers = /* @__PURE__ */ new Map();
function createResolver(rootPath, options) {
  function createResolverImpl(context, platform, rootPath2) {
    const baseExtensions = context.sourceExts.map((extension) => `.${extension}`);
    let finalExtensions = [...baseExtensions];
    if (context.preferNativePlatform) {
      finalExtensions = [...baseExtensions.map((extension) => `.native${extension}`), ...finalExtensions];
    }
    if (platform) {
      finalExtensions = [...baseExtensions.map((extension) => `.${platform}${extension}`), ...finalExtensions];
    }
    const resolver = import_enhanced_resolve.default.create.sync({
      extensions: finalExtensions,
      mainFields: context.mainFields,
      conditionNames: options?.conditionNames ?? [...import_constants.RESOLVER_EXPORTS_MAP_CONDITIONS, "require", "node", "default"],
      mainFiles: ["index"],
      modules: ["node_modules", import_path.default.join(rootPath2, "src")]
    });
    function resolve(context2, request) {
      for (const nativeModule of NATIVE_MODULES) {
        if (request === nativeModule) {
          return {
            type: "sourceFile",
            filePath: resolver({}, rootPath2, request)
          };
        }
      }
      for (const singletonModule of SINGLETON_MODULES) {
        if (request.startsWith(singletonModule)) {
          return {
            type: "sourceFile",
            filePath: resolver({}, rootPath2, request)
          };
        }
      }
      if (builtinModules.has(request)) {
        if (request in SUPPORTED_BUILTIN_FALLBACKS) {
          const source = SUPPORTED_BUILTIN_FALLBACKS[request];
          return {
            type: "sourceFile",
            filePath: require.resolve(`${source}/`)
          };
        }
        request = `${request}/`;
      }
      try {
        const resolveResult = resolver({}, import_path.default.dirname(context2.originModulePath), request);
        return {
          type: "sourceFile",
          filePath: resolveResult
        };
      } catch (error) {
        const typedError = error;
        if ("code" in typedError && typedError.code !== "QUALIFIED_PATH_RESOLUTION_FAILED") {
          typedError.message = `"${context2.originModulePath}"\uC5D0\uC11C "${request}"\uB97C \uCC3E\uC744 \uC218 \uC5C6\uC5C8\uC2B5\uB2C8\uB2E4.
${typedError.message}`;
        }
        throw typedError;
      }
    }
    return resolve;
  }
  return function resolve(context, request, platform) {
    let resolver = resolvers.get(platform);
    if (resolver == null) {
      resolver = createResolverImpl(context, platform, rootPath);
      resolvers.set(platform, resolver);
    }
    return resolver(context, request, platform);
  };
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  createResolver
});
