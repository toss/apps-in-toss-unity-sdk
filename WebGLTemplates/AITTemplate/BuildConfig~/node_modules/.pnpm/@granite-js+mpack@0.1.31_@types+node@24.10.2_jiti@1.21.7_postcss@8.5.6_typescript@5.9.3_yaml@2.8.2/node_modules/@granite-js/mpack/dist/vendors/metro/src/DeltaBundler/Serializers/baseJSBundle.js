"use strict";
const getAppendScripts = require("../../lib/getAppendScripts");
const processModules = require("./helpers/processModules");
function baseJSBundle(entryPoint, preModules, graph, options) {
  for (const module2 of graph.dependencies.values()) {
    options.createModuleId(module2.path);
  }
  const processModulesOptions = {
    filter: options.processModuleFilter,
    createModuleId: options.createModuleId,
    dev: options.dev,
    projectRoot: options.projectRoot
  };
  if (options.modulesOnly) {
    preModules = [];
  }
  const preCode = processModules(preModules, processModulesOptions).map(([_, code]) => code).join("\n");
  const modules = [...graph.dependencies.values()].sort(
    (a, b) => options.createModuleId(a.path) - options.createModuleId(b.path)
  );
  const postCode = processModules(
    getAppendScripts(entryPoint, [...preModules, ...modules], graph.importBundleNames, {
      asyncRequireModulePath: options.asyncRequireModulePath,
      createModuleId: options.createModuleId,
      getRunModuleStatement: options.getRunModuleStatement,
      inlineSourceMap: options.inlineSourceMap,
      projectRoot: options.projectRoot,
      runBeforeMainModule: options.runBeforeMainModule,
      runModule: options.runModule,
      serverRoot: options.serverRoot,
      sourceMapUrl: options.sourceMapUrl,
      sourceUrl: options.sourceUrl
    }),
    processModulesOptions
  ).map(([_, code]) => code).join("\n");
  return {
    pre: preCode,
    post: postCode,
    modules: processModules([...graph.dependencies.values()], processModulesOptions).map(([module2, code]) => [
      options.createModuleId(module2.path),
      code
    ])
  };
}
module.exports = baseJSBundle;
