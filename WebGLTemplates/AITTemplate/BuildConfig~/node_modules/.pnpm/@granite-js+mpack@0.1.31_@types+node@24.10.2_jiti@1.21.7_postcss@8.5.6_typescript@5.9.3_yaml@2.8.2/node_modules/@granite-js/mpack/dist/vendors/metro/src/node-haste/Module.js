"use strict";
const isAbsolutePath = require("absolute-path");
class Module {
  path;
  _moduleCache;
  _sourceCode;
  // $FlowFixMe[missing-local-annot]
  constructor(file, moduleCache) {
    if (!isAbsolutePath(file)) {
      throw new Error("Expected file to be absolute path but got " + file);
    }
    this.path = file;
    this._moduleCache = moduleCache;
  }
  getPackage() {
    return this._moduleCache.getPackageForModule(this);
  }
  invalidate() {
  }
}
module.exports = Module;
