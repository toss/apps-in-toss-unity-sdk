"use strict";
const Module = require("./Module");
const Package = require("./Package");
class ModuleCache {
  _getClosestPackage;
  _moduleCache;
  _packageCache;
  // Cache for "closest package.json" queries by module path.
  _packagePathByModulePath;
  // The inverse of _packagePathByModulePath.
  _modulePathsByPackagePath;
  constructor(options) {
    this._getClosestPackage = options.getClosestPackage;
    this._moduleCache = /* @__PURE__ */ Object.create(null);
    this._packageCache = /* @__PURE__ */ Object.create(null);
    this._packagePathByModulePath = /* @__PURE__ */ Object.create(null);
    this._modulePathsByPackagePath = /* @__PURE__ */ Object.create(null);
  }
  getModule(filePath) {
    if (!this._moduleCache[filePath]) {
      this._moduleCache[filePath] = new Module(filePath, this);
    }
    return this._moduleCache[filePath];
  }
  getPackage(filePath) {
    if (!this._packageCache[filePath]) {
      this._packageCache[filePath] = new Package({
        file: filePath
      });
    }
    return this._packageCache[filePath];
  }
  getPackageForModule(module2) {
    return this.getPackageOf(module2.path);
  }
  getPackageOf(modulePath) {
    let packagePath = this._packagePathByModulePath[modulePath];
    if (packagePath && this._packageCache[packagePath]) {
      return this._packageCache[packagePath];
    }
    packagePath = this._getClosestPackage(modulePath);
    if (!packagePath) {
      return null;
    }
    this._packagePathByModulePath[modulePath] = packagePath;
    const modulePaths = this._modulePathsByPackagePath[packagePath] ?? /* @__PURE__ */ new Set();
    modulePaths.add(modulePath);
    this._modulePathsByPackagePath[packagePath] = modulePaths;
    return this.getPackage(packagePath);
  }
  processFileChange(type, filePath) {
    if (this._moduleCache[filePath]) {
      this._moduleCache[filePath].invalidate();
      delete this._moduleCache[filePath];
    }
    if (this._packageCache[filePath]) {
      this._packageCache[filePath].invalidate();
      delete this._packageCache[filePath];
    }
    if (this._packagePathByModulePath[filePath]) {
      const packagePath = this._packagePathByModulePath[filePath];
      delete this._packagePathByModulePath[filePath];
      const modulePaths = this._modulePathsByPackagePath[packagePath];
      if (modulePaths) {
        modulePaths.delete(filePath);
        if (modulePaths.size === 0) {
          delete this._modulePathsByPackagePath[packagePath];
        }
      }
    }
    if (this._modulePathsByPackagePath[filePath]) {
      const modulePaths = this._modulePathsByPackagePath[filePath];
      for (const modulePath of modulePaths) {
        delete this._packagePathByModulePath[modulePath];
      }
      modulePaths.clear();
      delete this._modulePathsByPackagePath[filePath];
    }
  }
}
module.exports = ModuleCache;
