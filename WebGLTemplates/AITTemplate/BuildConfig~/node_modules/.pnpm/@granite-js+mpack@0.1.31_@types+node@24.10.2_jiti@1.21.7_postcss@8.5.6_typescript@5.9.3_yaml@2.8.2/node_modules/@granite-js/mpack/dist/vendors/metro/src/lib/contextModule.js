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
var contextModule_exports = {};
__export(contextModule_exports, {
  deriveAbsolutePathFromContext: () => deriveAbsolutePathFromContext,
  fileMatchesContext: () => fileMatchesContext
});
module.exports = __toCommonJS(contextModule_exports);
var import_crypto = __toESM(require("crypto"));
var import_path = __toESM(require("path"));
var import_nullthrows = __toESM(require("nullthrows"));
function toHash(value) {
  return import_crypto.default.createHash("sha1").update(value).digest("hex");
}
function deriveAbsolutePathFromContext(from, context) {
  const filePath = from.endsWith(import_path.default.sep) ? from.slice(0, -1) : from;
  return filePath + "?ctx=" + toHash(
    [
      context.mode,
      context.recursive ? "recursive" : "",
      new RegExp(context.filter.pattern, context.filter.flags).toString()
    ].filter(Boolean).join(" ")
  );
}
function fileMatchesContext(testPath, context) {
  const filePath = import_path.default.relative((0, import_nullthrows.default)(context.from), testPath);
  const filter = context.filter;
  if (
    // Ignore everything outside of the provided `root`.
    !(filePath && !filePath.startsWith("..")) || // Prevent searching in child directories during a non-recursive search.
    !context.recursive && filePath.includes(import_path.default.sep) || // Test against the filter.
    !filter.test(
      // NOTE(EvanBacon): Ensure files start with `./` for matching purposes
      // this ensures packages work across Metro and Webpack (ex: Storybook for React DOM / React Native).
      // `a/b.js` -> `./a/b.js`
      "./" + filePath.replace(/\\/g, "/")
    )
  ) {
    return false;
  }
  return true;
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  deriveAbsolutePathFromContext,
  fileMatchesContext
});
