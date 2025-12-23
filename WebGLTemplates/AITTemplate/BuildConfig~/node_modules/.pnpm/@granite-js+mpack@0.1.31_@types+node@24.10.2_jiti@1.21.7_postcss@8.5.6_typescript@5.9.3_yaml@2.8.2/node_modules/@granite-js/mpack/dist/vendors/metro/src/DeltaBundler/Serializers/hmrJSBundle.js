"use strict";
const { isJsModule, wrapModule } = require("./helpers/js");
const { addParamsToDefineCall } = require("metro-transform-plugins");
const path = require("path");
const url = require("url");
function generateModules(sourceModules, graph, options) {
  const modules = [];
  for (const module2 of sourceModules) {
    if (isJsModule(module2)) {
      const getURL = (extension) => {
        options.clientUrl.pathname = path.relative(
          options.projectRoot,
          path.join(path.dirname(module2.path), path.basename(module2.path, path.extname(module2.path)) + "." + extension)
        );
        return url.format(options.clientUrl);
      };
      const sourceMappingURL = getURL("map");
      const sourceURL = getURL("bundle");
      const code = prepareModule(module2, graph, options) + `
//# sourceMappingURL=${sourceMappingURL}
//# sourceURL=${sourceURL}
`;
      modules.push({
        module: [options.createModuleId(module2.path), code],
        sourceMappingURL,
        sourceURL
      });
    }
  }
  return modules;
}
function prepareModule(module2, graph, options) {
  const code = wrapModule(module2, {
    ...options,
    dev: true
  });
  const inverseDependencies = getInverseDependencies(module2.path, graph);
  const inverseDependenciesById = /* @__PURE__ */ Object.create(null);
  Object.keys(inverseDependencies).forEach((path2) => {
    inverseDependenciesById[options.createModuleId(path2)] = inverseDependencies[path2].map(options.createModuleId);
  });
  return addParamsToDefineCall(code, inverseDependenciesById);
}
function getInverseDependencies(path2, graph, inverseDependencies = {}) {
  if (path2 in inverseDependencies) {
    return inverseDependencies;
  }
  const module2 = graph.dependencies.get(path2);
  if (!module2) {
    return inverseDependencies;
  }
  inverseDependencies[path2] = [];
  for (const inverse of module2.inverseDependencies) {
    inverseDependencies[path2].push(inverse);
    getInverseDependencies(inverse, graph, inverseDependencies);
  }
  return inverseDependencies;
}
function hmrJSBundle(delta, graph, options) {
  return {
    added: generateModules(delta.added.values(), graph, options),
    modified: generateModules(delta.modified.values(), graph, options),
    deleted: [...delta.deleted].map((path2) => options.createModuleId(path2))
  };
}
module.exports = hmrJSBundle;
