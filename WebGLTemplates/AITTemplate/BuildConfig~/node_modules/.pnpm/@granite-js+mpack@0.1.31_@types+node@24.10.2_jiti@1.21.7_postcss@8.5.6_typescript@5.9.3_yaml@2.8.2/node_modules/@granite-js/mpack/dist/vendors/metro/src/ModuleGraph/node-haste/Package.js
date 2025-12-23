"use strict";
const nullthrows = require("nullthrows");
const path = require("path");
module.exports = class Package {
  data;
  path;
  root;
  type;
  constructor(packagePath, data) {
    this.data = data;
    this.path = packagePath;
    this.root = path.dirname(packagePath);
    this.type = "Package";
  }
  getMain() {
    const replacements = getReplacements(this.data);
    if (typeof replacements === "string") {
      return path.join(this.root, replacements);
    }
    let main = getMain(this.data);
    if (replacements && typeof replacements === "object") {
      main = replacements[main] || replacements[main + ".js"] || replacements[main + ".json"] || replacements[main.replace(/(\.js|\.json)$/, "")] || main;
    }
    return path.join(this.root, main);
  }
  getName() {
    return nullthrows(this.data.name);
  }
  isHaste() {
    return !!this.data.name;
  }
  redirectRequire(name) {
    const replacements = getReplacements(this.data);
    if (!replacements || typeof replacements !== "object") {
      return name;
    }
    if (!path.isAbsolute(name)) {
      const replacement = replacements[name];
      return replacement === false ? false : replacement || name;
    }
    let relPath = "./" + path.relative(this.root, name);
    if (path.sep !== "/") {
      relPath = relPath.replace(new RegExp("\\" + path.sep, "g"), "/");
    }
    let redirect = replacements[relPath];
    if (redirect == null) {
      redirect = replacements[relPath + ".js"];
      if (redirect == null) {
        redirect = replacements[relPath + ".json"];
      }
    }
    if (redirect === false) {
      return false;
    }
    if (redirect) {
      return path.join(this.root, redirect);
    }
    return name;
  }
};
function getMain(pkg) {
  return pkg.main || "index";
}
function getReplacements(pkg) {
  let rn = pkg["react-native"];
  let browser = pkg.browser;
  if (rn == null) {
    return browser;
  }
  if (browser == null) {
    return rn;
  }
  const main = getMain(pkg);
  if (typeof rn !== "object") {
    rn = { [main]: rn };
  }
  if (typeof browser !== "object") {
    browser = { [main]: browser };
  }
  return { ...browser, ...rn };
}
