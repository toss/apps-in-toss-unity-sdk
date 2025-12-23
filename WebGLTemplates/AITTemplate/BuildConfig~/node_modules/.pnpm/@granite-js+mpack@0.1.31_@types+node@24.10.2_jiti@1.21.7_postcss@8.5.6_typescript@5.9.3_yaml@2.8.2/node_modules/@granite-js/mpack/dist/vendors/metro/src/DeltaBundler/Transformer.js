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
var import_crypto = __toESM(require("crypto"));
const getTransformCacheKey = require("./getTransformCacheKey");
const WorkerFarm = require("./WorkerFarm");
const assert = require("assert");
const fs = require("fs");
const { Cache, stableHash } = require("metro-cache");
const path = require("path");
class Transformer {
  _config;
  _cache;
  _baseHash;
  _getSha1;
  _workerFarm;
  constructor(config, getSha1Fn) {
    this._config = config;
    this._config.watchFolders.forEach(verifyRootExists);
    this._cache = new Cache(config.cacheStores);
    this._getSha1 = getSha1Fn;
    const {
      getTransformOptions: _getTransformOptions,
      transformVariants: _transformVariants,
      workerPath: _workerPath,
      ...transformerConfig
    } = this._config.transformer;
    const transformerOptions = {
      transformerPath: this._config.transformerPath,
      transformerConfig
    };
    this._workerFarm = new WorkerFarm(config, transformerOptions);
    const globalCacheKey = this._cache.isDisabled ? "" : getTransformCacheKey({
      cacheVersion: this._config.cacheVersion,
      projectRoot: this._config.projectRoot,
      transformerConfig: transformerOptions
    });
    this._baseHash = stableHash([globalCacheKey]).toString("binary");
  }
  async transformFile(filePath, transformerOptions, fileBuffer) {
    const cache = this._cache;
    const {
      customTransformOptions,
      dev,
      experimentalImportSupport,
      hot,
      inlinePlatform,
      inlineRequires,
      minify,
      nonInlinedRequires,
      platform,
      runtimeBytecodeVersion,
      type,
      unstable_disableES6Transforms,
      unstable_transformProfile,
      ...extra
    } = transformerOptions;
    for (const key in extra) {
      if (hasOwnProperty.call(extra, key)) {
        throw new Error("Extra keys detected: " + Object.keys(extra).join(", "));
      }
    }
    const localPath = path.relative(this._config.projectRoot, filePath);
    const partialKey = stableHash([
      // This is the hash related to the global Bundler config.
      this._baseHash,
      // Path.
      localPath,
      customTransformOptions,
      dev,
      experimentalImportSupport,
      hot,
      inlinePlatform,
      inlineRequires,
      minify,
      nonInlinedRequires,
      platform,
      runtimeBytecodeVersion,
      type,
      unstable_disableES6Transforms,
      unstable_transformProfile
    ]);
    let sha1;
    if (fileBuffer) {
      sha1 = import_crypto.default.createHash("sha1").update(fileBuffer).digest("hex");
    } else {
      sha1 = this._getSha1(filePath);
    }
    let fullKey = Buffer.concat([partialKey, Buffer.from(sha1, "hex")]);
    const result = await cache.get(fullKey);
    const data = result ? { result, sha1 } : await this._workerFarm.transform(localPath, transformerOptions, fileBuffer);
    if (sha1 !== data.sha1) {
      fullKey = Buffer.concat([partialKey, Buffer.from(data.sha1, "hex")]);
    }
    cache.set(fullKey, data.result);
    return {
      ...data.result,
      getSource() {
        if (fileBuffer) {
          return fileBuffer;
        }
        return fs.readFileSync(filePath);
      }
    };
  }
  end() {
    this._workerFarm.kill();
  }
}
function verifyRootExists(root) {
  assert(fs.statSync(root).isDirectory(), "Root has to be a valid directory");
}
module.exports = Transformer;
