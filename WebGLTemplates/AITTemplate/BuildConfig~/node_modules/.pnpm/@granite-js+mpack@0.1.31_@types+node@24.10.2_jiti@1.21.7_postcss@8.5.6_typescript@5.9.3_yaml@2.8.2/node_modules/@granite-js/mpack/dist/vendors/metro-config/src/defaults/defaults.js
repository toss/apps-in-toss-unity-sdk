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
const defaultCreateModuleIdFactory = require("../../../metro/src/lib/createModuleIdFactory");
exports.assetExts = [
  // Image formats
  "bmp",
  "gif",
  "jpg",
  "jpeg",
  "png",
  "psd",
  "svg",
  "webp",
  // Video formats
  "m4v",
  "mov",
  "mp4",
  "mpeg",
  "mpg",
  "webm",
  // Audio formats
  "aac",
  "aiff",
  "caf",
  "m4a",
  "mp3",
  "wav",
  // Document formats
  "html",
  "pdf",
  "yaml",
  "yml",
  // Font formats
  "otf",
  "ttf",
  // Archives (virtual files)
  "zip"
];
exports.assetResolutions = ["1", "1.5", "2", "3", "4"];
exports.sourceExts = ["js", "jsx", "json", "ts", "tsx"];
exports.additionalExts = ["cjs", "mjs"];
exports.moduleSystem = require.resolve("metro-runtime/src/polyfills/require.js");
exports.platforms = ["ios", "android", "windows", "web"];
exports.DEFAULT_METRO_MINIFIER_PATH = "metro-minify-uglify";
exports.defaultCreateModuleIdFactory = defaultCreateModuleIdFactory;
