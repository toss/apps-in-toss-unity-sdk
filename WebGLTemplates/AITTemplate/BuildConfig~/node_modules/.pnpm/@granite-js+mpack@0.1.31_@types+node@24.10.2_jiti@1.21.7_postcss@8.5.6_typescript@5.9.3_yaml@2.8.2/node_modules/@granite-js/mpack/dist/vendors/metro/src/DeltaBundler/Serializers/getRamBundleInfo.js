"use strict";
const { createRamBundleGroups } = require("../../Bundler/util");
const getAppendScripts = require("../../lib/getAppendScripts");
const getTransitiveDependencies = require("./helpers/getTransitiveDependencies");
const { isJsModule, wrapModule } = require("./helpers/js");
const { sourceMapObject } = require("./sourceMapObject");
const nullthrows = require("nullthrows");
const path = require("path");
async function getRamBundleInfo(entryPoint, pre, graph, options) {
  let modules = [...pre, ...graph.dependencies.values()];
  modules = modules.concat(getAppendScripts(entryPoint, modules, graph.importBundleNames, options));
  modules.forEach((module2) => options.createModuleId(module2.path));
  const ramModules = modules.filter(isJsModule).filter(options.processModuleFilter).map((module2) => ({
    id: options.createModuleId(module2.path),
    code: wrapModule(module2, options),
    map: sourceMapObject([module2], {
      excludeSource: options.excludeSource,
      processModuleFilter: options.processModuleFilter
    }),
    name: path.basename(module2.path),
    sourcePath: module2.path,
    source: module2.getSource().toString(),
    type: nullthrows(module2.output.find(({ type }) => type.startsWith("js"))).type
  }));
  const { preloadedModules, ramGroups } = await _getRamOptions(
    entryPoint,
    {
      dev: options.dev,
      platform: options.platform
    },
    (filePath) => getTransitiveDependencies(filePath, graph),
    options.getTransformOptions
  );
  const startupModules = [];
  const lazyModules = [];
  ramModules.forEach((module2) => {
    if (preloadedModules.hasOwnProperty(module2.sourcePath)) {
      startupModules.push(module2);
      return;
    }
    if (module2.type.startsWith("js/script")) {
      startupModules.push(module2);
      return;
    }
    if (module2.type.startsWith("js/module")) {
      lazyModules.push(module2);
    }
  });
  const groups = createRamBundleGroups(ramGroups, lazyModules, (module2, dependenciesByPath) => {
    const deps = getTransitiveDependencies(module2.sourcePath, graph);
    const output = /* @__PURE__ */ new Set();
    for (const dependency of deps) {
      const module3 = dependenciesByPath.get(dependency);
      if (module3) {
        output.add(module3.id);
      }
    }
    return output;
  });
  return {
    getDependencies: (filePath) => getTransitiveDependencies(filePath, graph),
    groups,
    lazyModules,
    startupModules
  };
}
async function _getRamOptions(entryFile, options, getDependencies, getTransformOptions) {
  if (getTransformOptions == null) {
    return {
      preloadedModules: {},
      ramGroups: []
    };
  }
  const { preloadedModules, ramGroups } = await getTransformOptions(
    [entryFile],
    { dev: options.dev, hot: true, platform: options.platform },
    /* $FlowFixMe(>=0.99.0 site=react_native_fb) This comment suppresses an
     * error found when Flow v0.99 was deployed. To see the error, delete this
     * comment and run Flow. */
    async (x) => Array.from(getDependencies)
  );
  return {
    preloadedModules: preloadedModules || {},
    ramGroups: ramGroups || []
  };
}
module.exports = getRamBundleInfo;
