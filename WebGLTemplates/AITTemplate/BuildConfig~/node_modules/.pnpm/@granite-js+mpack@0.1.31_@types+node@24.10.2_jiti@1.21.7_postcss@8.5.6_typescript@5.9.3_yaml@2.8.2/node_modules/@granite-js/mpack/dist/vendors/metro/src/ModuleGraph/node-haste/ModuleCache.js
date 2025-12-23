"use strict";
const Module = require("./Module");
const Package = require("./Package");
module.exports = class ModuleCache {
  _getClosestPackage;
  getTransformedFile;
  modules;
  packages;
  constructor(getClosestPackage, getTransformedFile) {
    this._getClosestPackage = getClosestPackage;
    this.getTransformedFile = getTransformedFile;
    this.modules = /* @__PURE__ */ new Map();
    this.packages = /* @__PURE__ */ new Map();
  }
  getModule(path) {
    const normalizedPath = path.startsWith("//") ? path.substr(1) : path;
    let m = this.modules.get(normalizedPath);
    if (!m) {
      m = new Module(normalizedPath, this, this.getTransformedFile(normalizedPath));
      this.modules.set(normalizedPath, m);
    }
    return m;
  }
  getPackage(path) {
    let p = this.packages.get(path);
    if (!p) {
      p = new Package(path, this.getPackageData(path));
      this.packages.set(path, p);
    }
    return p;
  }
  getPackageData(path) {
    const pkg = this.getTransformedFile(path).package;
    if (!pkg) {
      throw new Error(`"${path}" does not exist`);
    }
    return pkg;
  }
  getPackageOf(filePath) {
    const candidate = this._getClosestPackage(filePath);
    return candidate != null ? this.getPackage(candidate) : null;
  }
};
