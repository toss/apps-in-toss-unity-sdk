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
var import_template = __toESM(require("@babel/template"));
var import_traverse = __toESM(require("@babel/traverse"));
var t = __toESM(require("@babel/types"));
var import_invariant = __toESM(require("invariant"));
const WRAP_NAME = "$$_REQUIRE";
const IIFE_PARAM = import_template.default.expression(
  "typeof globalThis !== 'undefined' ? globalThis : typeof global !== 'undefined' ? global : typeof window !== 'undefined' ? window : this"
);
function wrapModule(fileAst, importDefaultName, importAllName, dependencyMapName, globalPrefix) {
  const params = buildParameters(importDefaultName, importAllName, dependencyMapName);
  const factory = functionFromProgram(fileAst.program, params);
  const def = t.callExpression(t.identifier(`${globalPrefix}__d`), [factory]);
  const ast = t.file(t.program([t.expressionStatement(def)]));
  const requireName = renameRequires(ast);
  return { ast, requireName };
}
function wrapPolyfill(fileAst) {
  const factory = functionFromProgram(fileAst.program, ["global"]);
  const iife = t.callExpression(factory, [IIFE_PARAM()]);
  return t.file(t.program([t.expressionStatement(iife)]));
}
function jsonToCommonJS(source) {
  return `module.exports = ${source};`;
}
function wrapJson(source, globalPrefix) {
  const moduleFactoryParameters = buildParameters("_importDefaultUnused", "_importAllUnused", "_dependencyMapUnused");
  return [
    `${globalPrefix}__d(function(${moduleFactoryParameters.join(", ")}) {`,
    `  ${jsonToCommonJS(source)}`,
    "});"
  ].join("\n");
}
function functionFromProgram(program, parameters) {
  return t.functionExpression(
    void 0,
    parameters.map(makeIdentifier),
    t.blockStatement(program.body, program.directives)
  );
}
function makeIdentifier(name) {
  return t.identifier(name);
}
function buildParameters(importDefaultName, importAllName, dependencyMapName) {
  return ["global", "require", importDefaultName, importAllName, "module", "exports", dependencyMapName];
}
function renameRequires(ast) {
  let newRequireName = WRAP_NAME;
  (0, import_traverse.default)(ast, {
    Program(path) {
      const body = path.get("body.0.expression.arguments.0.body");
      (0, import_invariant.default)(!Array.isArray(body), "metro: Expected `body` to be a single path.");
      newRequireName = body.scope.generateUid(WRAP_NAME);
      body.scope.rename("require", newRequireName);
    }
  });
  return newRequireName;
}
module.exports = {
  WRAP_NAME,
  wrapJson,
  jsonToCommonJS,
  wrapModule,
  wrapPolyfill
};
