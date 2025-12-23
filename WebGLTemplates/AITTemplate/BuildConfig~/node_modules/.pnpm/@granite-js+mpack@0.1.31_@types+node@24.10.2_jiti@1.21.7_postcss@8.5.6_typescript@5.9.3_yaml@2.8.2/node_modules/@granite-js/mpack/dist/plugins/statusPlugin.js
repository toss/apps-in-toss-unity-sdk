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
var statusPlugin_exports = {};
__export(statusPlugin_exports, {
  statusPlugin: () => statusPlugin
});
module.exports = __toCommonJS(statusPlugin_exports);
var import_es_toolkit = require("es-toolkit");
var import_PersistentStorage = require("../shared/PersistentStorage");
var import_printSummary = require("../utils/printSummary");
var import_progressBar = require("../utils/progressBar");
function statusPlugin(handlers) {
  let progressBar;
  let totalModuleCount;
  return {
    name: "dev-server-status-plugin",
    prepare(config) {
      if (progressBar != null) {
        return;
      }
      progressBar = (0, import_progressBar.createProgressBar)(config.buildConfig.platform);
      totalModuleCount = import_PersistentStorage.persistentStorage.getData()[this.id]?.totalModuleCount;
    },
    buildStart() {
      progressBar?.start(0, totalModuleCount ?? 1);
      handlers?.onStart?.();
    },
    buildEnd(result) {
      if ("bundle" in result) {
        totalModuleCount = result.totalModuleCount;
        import_PersistentStorage.persistentStorage.setData({ [this.id]: { totalModuleCount: result.totalModuleCount } });
        import_PersistentStorage.persistentStorage.saveData();
      } else {
        totalModuleCount = void 0;
      }
      progressBar.hide();
      progressBar.remove();
      handlers?.onEnd?.();
      (0, import_es_toolkit.delay)(100).then(() => (0, import_printSummary.printSummary)(result)).catch(import_es_toolkit.noop);
    },
    load: (moduleCount) => {
      progressBar.update(moduleCount, totalModuleCount);
    }
  };
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  statusPlugin
});
