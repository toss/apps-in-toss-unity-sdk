"use strict";
const { getAssetFiles } = require("../../Assets");
const { getJsOutput, isJsModule } = require("./helpers/js");
async function getAllFiles(pre, graph, options) {
  const modules = graph.dependencies;
  const { processModuleFilter } = options;
  const promises = [];
  for (const module2 of pre) {
    if (processModuleFilter(module2)) {
      promises.push([module2.path]);
    }
  }
  for (const module2 of modules.values()) {
    if (!isJsModule(module2) || !processModuleFilter(module2)) {
      continue;
    }
    if (getJsOutput(module2).type === "js/module/asset") {
      promises.push(getAssetFiles(module2.path, options.platform));
    } else {
      promises.push([module2.path]);
    }
  }
  const dependencies = await Promise.all(promises);
  const output = [];
  for (const dependencyArray of dependencies) {
    output.push(...dependencyArray);
  }
  return output;
}
module.exports = getAllFiles;
