"use strict";
const getAppendScripts = require("../../lib/getAppendScripts");
const { getJsOutput } = require("./helpers/js");
const processBytecodeModules = require("./helpers/processBytecodeModules");
function baseBytecodeBundle(entryPoint, preModules, graph, options) {
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
  const modules = [...graph.dependencies.values()].sort(
    (a, b) => options.createModuleId(a.path) - options.createModuleId(b.path)
  );
  const { compile } = require("metro-hermes-compiler");
  const post = processBytecodeModules(
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
    }).map((module2) => {
      return {
        ...module2,
        output: [
          ...module2.output,
          {
            type: "bytecode/script/virtual",
            data: {
              bytecode: compile(getJsOutput(module2).data.code, {
                sourceURL: module2.path
              }).bytecode
            }
          }
        ]
      };
    }),
    processModulesOptions
  ).flatMap(([module2, bytecodeBundle]) => bytecodeBundle);
  const processedModules = processBytecodeModules([...graph.dependencies.values()], processModulesOptions).map(
    ([module2, bytecodeBundle]) => [options.createModuleId(module2.path), bytecodeBundle]
  );
  return {
    pre: processBytecodeModules(preModules, processModulesOptions).flatMap(([_, bytecodeBundle]) => bytecodeBundle),
    post,
    modules: processedModules
  };
}
module.exports = baseBytecodeBundle;
