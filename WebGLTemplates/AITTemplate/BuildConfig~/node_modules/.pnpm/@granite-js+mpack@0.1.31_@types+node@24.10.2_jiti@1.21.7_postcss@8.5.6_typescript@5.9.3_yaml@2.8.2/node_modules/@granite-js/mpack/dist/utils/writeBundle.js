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
var writeBundle_exports = {};
__export(writeBundle_exports, {
  writeBundle: () => writeBundle,
  writeMetafile: () => writeMetafile
});
module.exports = __toCommonJS(writeBundle_exports);
var fs = __toESM(require("fs/promises"));
var path = __toESM(require("path"));
var import_getSourcemapName = require("./getSourcemapName");
async function writeBundle(outputPath, { source, sourcemap }) {
  await createDirectories(path.dirname(outputPath));
  const baseDirectory = path.dirname(outputPath);
  const basename = path.basename(outputPath);
  await Promise.all([
    fs.writeFile(outputPath, source.contents, "utf-8"),
    fs.writeFile(path.join(baseDirectory, (0, import_getSourcemapName.getSourcemapName)(basename)), sourcemap.contents, "utf-8")
  ]);
}
async function writeMetafile(outputPath, metafile) {
  const outputDir = path.dirname(outputPath);
  const extname = path.extname(outputPath);
  const destination = path.join(outputDir, `${path.basename(outputPath, extname)}-meta.json`);
  await createDirectories(outputDir);
  await fs.writeFile(destination, JSON.stringify(metafile, null, 2), "utf-8");
}
function createDirectories(directoryPath) {
  return fs.mkdir(directoryPath, { recursive: true });
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  writeBundle,
  writeMetafile
});
