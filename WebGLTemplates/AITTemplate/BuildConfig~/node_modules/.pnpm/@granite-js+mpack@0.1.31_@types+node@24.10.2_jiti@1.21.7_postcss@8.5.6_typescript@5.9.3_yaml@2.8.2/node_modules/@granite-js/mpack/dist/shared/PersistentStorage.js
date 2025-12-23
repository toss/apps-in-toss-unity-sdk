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
var PersistentStorage_exports = {};
__export(PersistentStorage_exports, {
  persistentStorage: () => persistentStorage
});
module.exports = __toCommonJS(PersistentStorage_exports);
var fs = __toESM(require("fs"));
var path = __toESM(require("path"));
var import_es_toolkit = require("es-toolkit");
var import_constants = require("../constants");
class PersistentStorage {
  data = {};
  constructor() {
    this.loadData();
  }
  getData() {
    return this.data;
  }
  setData(newData) {
    this.data = (0, import_es_toolkit.toMerged)(this.data, newData);
  }
  loadData() {
    try {
      fs.accessSync(import_constants.MPACK_DATA_DIR, fs.constants.R_OK | fs.constants.W_OK);
    } catch {
      fs.mkdirSync(import_constants.MPACK_DATA_DIR, { recursive: true });
    }
    try {
      this.data = JSON.parse(fs.readFileSync(path.join(import_constants.MPACK_DATA_DIR, ".mpack"), "utf-8"));
    } catch {
    }
  }
  async saveData() {
    try {
      await fs.promises.writeFile(path.join(import_constants.MPACK_DATA_DIR, ".mpack"), JSON.stringify(this.data, null, 2), "utf-8");
    } catch {
    }
  }
}
const persistentStorage = new PersistentStorage();
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  persistentStorage
});
