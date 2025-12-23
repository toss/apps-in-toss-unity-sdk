"use strict";
const { getAssetData } = require("../../Assets");
const { getJsOutput, isJsModule } = require("./helpers/js");
const path = require("path");
async function getAssets(dependencies, options) {
  const promises = [];
  const { processModuleFilter } = options;
  for (const module2 of dependencies.values()) {
    if (isJsModule(module2) && processModuleFilter(module2) && getJsOutput(module2).type === "js/module/asset" && path.relative(options.projectRoot, module2.path) !== "package.json") {
      promises.push(
        getAssetData(
          module2.path,
          path.relative(options.projectRoot, module2.path),
          options.assetPlugins,
          options.platform,
          options.publicPath
        )
      );
    }
  }
  return await Promise.all(promises);
}
module.exports = getAssets;
