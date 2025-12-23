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
var preludeScript_exports = {};
__export(preludeScript_exports, {
  injectPreludeScript: () => injectPreludeScript,
  isEntryPoint: () => isEntryPoint,
  registerEntryPointMarker: () => registerEntryPointMarker,
  registerPreludeScriptResolver: () => registerPreludeScriptResolver
});
module.exports = __toCommonJS(preludeScript_exports);
var path = __toESM(require("path"));
var import_es_toolkit = require("es-toolkit");
var import_constants = require("../../../../constants");
var import_esbuildUtils = require("../../../../utils/esbuildUtils");
var import_resolveHelpers = require("../../resolveHelpers");
const IS_ENTRY_FLAG = Symbol.for("mpack:IS_ENTRY_FLAG");
function registerEntryPointMarker(build) {
  const resolver = (0, import_resolveHelpers.createNonRecursiveResolver)(build);
  build.onResolve({ filter: /\.([mc]js|[tj]sx?)$/ }, async (args) => {
    if (args.kind !== "entry-point") {
      return null;
    }
    const result = await resolver(args, {
      importer: args.importer,
      kind: args.kind,
      resolveDir: args.resolveDir
    });
    return result ? { ...result, pluginData: IS_ENTRY_FLAG } : null;
  });
}
function registerPreludeScriptResolver(build) {
  build.onResolve({ filter: new RegExp(`^${import_constants.PRELUDE_PROTOCOL}.*`) }, (args) => {
    const basePath = args.path.slice(import_constants.PRELUDE_PROTOCOL.length);
    const resolvePath = path.resolve(args.resolveDir, basePath);
    return { path: resolvePath, sideEffects: true };
  });
}
function isEntryPoint(args) {
  return args.pluginData === IS_ENTRY_FLAG;
}
function injectPreludeScript(code, {
  preludeScriptPaths
}) {
  return [...preludeScriptPaths.map((path2) => `import '${import_constants.PRELUDE_PROTOCOL}${(0, import_esbuildUtils.normalizePath)(path2)}';`), code].filter(import_es_toolkit.isNotNil).join("\n");
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  injectPreludeScript,
  isEntryPoint,
  registerEntryPointMarker,
  registerPreludeScriptResolver
});
