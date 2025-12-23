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
var getMonorepoRoot_exports = {};
__export(getMonorepoRoot_exports, {
  getMonorepoRoot: () => getMonorepoRoot
});
module.exports = __toCommonJS(getMonorepoRoot_exports);
var import_promises = __toESM(require("fs/promises"));
var import_path = __toESM(require("path"));
var import_pnpapi = require("./pnpapi");
async function getMonorepoRoot(basePath) {
  if (import_pnpapi.pnpapi) {
    return import_pnpapi.pnpapi.getPackageInformation(import_pnpapi.pnpapi.topLevel).packageLocation ?? null;
  }
  let curr = basePath;
  while (curr !== import_path.default.dirname(curr)) {
    if (await isWorkspace(basePath)) {
      return curr;
    }
    curr = import_path.default.dirname(curr);
  }
  return basePath;
}
async function safeReadPackageJson(basePath) {
  try {
    return JSON.parse(await import_promises.default.readFile(import_path.default.join(basePath, "package.json"), "utf-8"));
  } catch {
  }
}
async function safeAccess(path2) {
  try {
    await import_promises.default.access(path2, import_promises.constants.R_OK);
    return true;
  } catch {
    return false;
  }
}
async function isWorkspace(basePath) {
  const packageJson = await safeReadPackageJson(basePath);
  const hasPackageJson = Boolean(packageJson);
  const hasWorkspacesField = Array.isArray(packageJson?.workspaces);
  if (hasWorkspacesField) {
    return true;
  }
  const hasWorkspaceManifest = await safeAccess(import_path.default.join(basePath, "pnpm-workspace.yaml"));
  if (hasPackageJson && hasWorkspaceManifest) {
    return true;
  }
  return false;
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  getMonorepoRoot
});
