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
var setupAliasResolver_exports = {};
__export(setupAliasResolver_exports, {
  setupAliasResolver: () => setupAliasResolver
});
module.exports = __toCommonJS(setupAliasResolver_exports);
var import_path = __toESM(require("path"));
var import_es_toolkit = require("es-toolkit");
var import_performance = require("../../../../performance");
var import_esbuildUtils = require("../../../../utils/esbuildUtils");
var import_replaceModulePath = require("../../../../utils/replaceModulePath");
var import_resolveHelpers = require("../../resolveHelpers");
var import_swc = require("../../shared/swc");
function setupAliasResolver(build, aliasConfig) {
  const resolver = (0, import_resolveHelpers.createNonRecursiveResolver)(build);
  [import_swc.swcHelperOptimizationRules.getAliasConfig(), ...aliasConfig].forEach((aliasConfig2) => {
    const resolveResultCache = /* @__PURE__ */ new Map();
    const { filter, resolveAlias } = resolveAliasConfig(build, aliasConfig2);
    build.onResolve({ filter }, async (args) => {
      if ((0, import_resolveHelpers.isResolved)(args)) {
        return null;
      }
      const trace = import_performance.Performance.trace("alias-resolver", {
        detail: { pattern: filter, path: args.path }
      });
      const defaultResolveOptions = {
        resolveDir: args.resolveDir,
        importer: args.importer,
        kind: args.kind,
        with: args.with
      };
      const resolveResult = await resolveAlias(args);
      const resolvePath = resolveResult.path;
      const resolveOptions = { ...defaultResolveOptions, ...resolveResult.options ?? {} };
      const cacheKey = `${resolveOptions.kind}:${resolveOptions.resolveDir}:${resolvePath}`;
      if (resolveResultCache.has(cacheKey)) {
        trace.stop({ detail: { cacheHit: true } });
        return resolveResultCache.get(cacheKey);
      }
      if (import_path.default.isAbsolute(resolvePath)) {
        trace.stop({ detail: { isAbsolute: true } });
        const result2 = { path: resolvePath };
        resolveResultCache.set(cacheKey, result2);
        return result2;
      }
      const pathOverriddenArgs = { ...args, path: resolvePath };
      const result = await resolver(pathOverriddenArgs, resolveOptions);
      if (result) {
        trace.stop({ detail: { cacheHit: false, isAbsolute: false } });
        resolveResultCache.set(cacheKey, result);
        return result;
      }
      return null;
    });
  });
}
function resolveAliasConfig(build, aliasConfig) {
  const { from, to, exact } = aliasConfig;
  const escapedFrom = escapeRegExpString(from);
  const filter = new RegExp(exact ? `^${escapedFrom}$` : `^${escapedFrom}(?:$|/)`);
  const resolver = (0, import_resolveHelpers.createNonRecursiveResolver)(build);
  const aliasResolver = (boundArgs, path2, options) => {
    const result = resolver({ ...boundArgs, path: path2 }, options);
    (0, import_es_toolkit.assert)(result, "resolver should return result");
    return result;
  };
  const resolveAlias = async (args) => {
    if (typeof to === "string") {
      return normalizeResolveResult((0, import_replaceModulePath.replaceModulePath)(args.path, from, to));
    }
    if (typeof to === "function") {
      const result = await to({ resolve: aliasResolver.bind(null, args), args });
      return normalizeResolveResult(result);
    }
    return normalizeResolveResult(to);
  };
  return { filter, resolveAlias };
}
function escapeRegExpString(str) {
  return str.replace(/[|\\{}()[\]^$+*?.]/g, "\\$&");
}
function normalizeResolveResult(result) {
  if (typeof result === "string") {
    return { path: (0, import_esbuildUtils.normalizePath)(result), options: void 0 };
  }
  const { path: path2, ...options } = result;
  return { path: path2, options };
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  setupAliasResolver
});
