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
var import_invariant = __toESM(require("invariant"));
var import_nullthrows = __toESM(require("nullthrows"));
function reverseDependencyMapReferences({ types: t }) {
  return {
    visitor: {
      CallExpression(path, state) {
        const { node } = path;
        if (node.callee.name === `${state.opts.globalPrefix}__d`) {
          const lastArg = node.arguments[0].params.slice(-1)[0];
          const depMapName = lastArg && lastArg.name;
          if (depMapName == null) {
            return;
          }
          const body = path.get("arguments.0.body");
          (0, import_invariant.default)(!Array.isArray(body), "meetro: Expected `body` to be a single path.");
          const scope = body.scope;
          const binding = (0, import_nullthrows.default)(scope.getBinding(depMapName));
          binding.referencePaths.forEach(({ parentPath }) => {
            const memberNode = parentPath?.node;
            if (memberNode != null && memberNode.type === "MemberExpression" && memberNode.property.type === "NumericLiteral") {
              const numericLiteral = t.numericLiteral(state.opts.dependencyIds[memberNode.property.value]);
              (0, import_nullthrows.default)(parentPath).replaceWith(numericLiteral);
            }
          });
        }
      }
    }
  };
}
module.exports = reverseDependencyMapReferences;
