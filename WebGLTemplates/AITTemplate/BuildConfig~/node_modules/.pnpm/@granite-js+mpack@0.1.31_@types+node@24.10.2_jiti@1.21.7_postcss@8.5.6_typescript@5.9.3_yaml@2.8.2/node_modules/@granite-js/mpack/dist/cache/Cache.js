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
var Cache_exports = {};
__export(Cache_exports, {
  Cache: () => Cache
});
module.exports = __toCommonJS(Cache_exports);
var fs = __toESM(require("fs"));
var path = __toESM(require("path"));
var import_constants = require("../constants");
class Cache {
  constructor(cacheDirectoryName, config) {
    this.config = config;
    this.cachePath = path.join(Cache.BASE_CACHE_DIR, cacheDirectoryName);
    try {
      fs.accessSync(this.cachePath, fs.constants.R_OK | fs.constants.W_OK);
    } catch {
      fs.mkdirSync(this.cachePath, { recursive: true });
    }
  }
  static BASE_CACHE_DIR = import_constants.MPACK_CACHE_DIR;
  cache = {};
  cachePath;
  getDir() {
    return this.cachePath;
  }
  getMemoryCache() {
    return this.cache;
  }
  async read(key) {
    const cache = this.cache[key];
    try {
      if (cache) {
        return cache;
      }
      const fsCache = await this.readFromFileSystem(key);
      const parsedCache = this.config.parse(fsCache);
      this.writeToMemory(key, parsedCache);
      return parsedCache;
    } catch {
      return null;
    }
  }
  async write(key, value) {
    this.writeToMemory(key, value);
    await this.writeToFileSystem(key, value);
  }
  readFromFileSystem(key) {
    return fs.promises.readFile(path.join(this.getDir(), key), "utf-8");
  }
  writeToMemory(key, value) {
    this.cache[key] = value;
  }
  writeToFileSystem(key, value) {
    return fs.promises.writeFile(path.join(this.getDir(), key), this.config.stringify(value), "utf-8");
  }
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  Cache
});
