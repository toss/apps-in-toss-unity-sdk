"use strict";
var __defProp = Object.defineProperty;
var __getOwnPropDesc = Object.getOwnPropertyDescriptor;
var __getOwnPropNames = Object.getOwnPropertyNames;
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
var __toCommonJS = (mod) => __copyProps(__defProp({}, "__esModule", { value: true }), mod);
var PluginDriver_exports = {};
__export(PluginDriver_exports, {
  PluginDriver: () => PluginDriver
});
module.exports = __toCommonJS(PluginDriver_exports);
class PluginDriver {
  constructor(id) {
    this.id = id;
  }
  plugins = [];
  addPlugin(plugin) {
    this.plugins.push(plugin);
  }
  hookSync(hookName, parameters) {
    this.runHook(hookName, parameters);
  }
  runHook(hookName, parameters) {
    for (const plugin of this.plugins) {
      const handler = plugin[hookName];
      if (typeof handler === "function") {
        handler.apply({ id: this.id }, parameters);
      }
    }
  }
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  PluginDriver
});
