"use strict";
function getTransitiveDependencies(path, graph) {
  const dependencies = _getDeps(path, graph, /* @__PURE__ */ new Set());
  dependencies.delete(path);
  return dependencies;
}
function _getDeps(path, graph, deps) {
  if (deps.has(path)) {
    return deps;
  }
  const module2 = graph.dependencies.get(path);
  if (!module2) {
    return deps;
  }
  deps.add(path);
  for (const dependency of module2.dependencies.values()) {
    _getDeps(dependency.absolutePath, graph, deps);
  }
  return deps;
}
module.exports = getTransitiveDependencies;
