"use strict";
var __create = Object.create;
var __defProp = Object.defineProperty;
var __getOwnPropDesc = Object.getOwnPropertyDescriptor;
var __getOwnPropNames = Object.getOwnPropertyNames;
var __getProtoOf = Object.getPrototypeOf;
var __hasOwnProp = Object.prototype.hasOwnProperty;
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
var import_CountingSet = __toESM(require("../lib/CountingSet"));
var import_contextModule = require("../lib/contextModule");
var path = __toESM(require("path"));
const invariant = require("invariant");
const nullthrows = require("nullthrows");
function createGraph(options) {
  return {
    ...options,
    dependencies: /* @__PURE__ */ new Map(),
    importBundleNames: /* @__PURE__ */ new Set(),
    privateState: {
      resolvedContexts: /* @__PURE__ */ new Map(),
      gc: {
        color: /* @__PURE__ */ new Map(),
        possibleCycleRoots: /* @__PURE__ */ new Set(),
        importBundleRefs: /* @__PURE__ */ new Map()
      }
    }
  };
}
function getInternalOptions({ transform, resolve, onProgress, experimentalImportBundleSupport, shallow }) {
  let numProcessed = 0;
  let total = 0;
  return {
    experimentalImportBundleSupport,
    transform,
    resolve,
    onDependencyAdd: () => onProgress && onProgress(numProcessed, ++total),
    onDependencyAdded: () => onProgress && onProgress(++numProcessed, total),
    shallow
  };
}
async function traverseDependencies(paths, graph, options) {
  const delta = {
    added: /* @__PURE__ */ new Set(),
    modified: /* @__PURE__ */ new Set(),
    deleted: /* @__PURE__ */ new Set(),
    earlyInverseDependencies: /* @__PURE__ */ new Map()
  };
  const internalOptions = getInternalOptions(options);
  for (const path2 of paths) {
    if (graph.dependencies.get(path2)) {
      delta.modified.add(path2);
      await traverseDependenciesForSingleFile(path2, graph, delta, internalOptions);
    }
  }
  collectCycles(graph, delta);
  const added = /* @__PURE__ */ new Map();
  for (const path2 of delta.added) {
    added.set(path2, nullthrows(graph.dependencies.get(path2)));
  }
  const modified = /* @__PURE__ */ new Map();
  for (const path2 of delta.modified) {
    if (!delta.added.has(path2)) {
      modified.set(path2, nullthrows(graph.dependencies.get(path2)));
    }
  }
  return {
    added,
    modified,
    deleted: delta.deleted
  };
}
async function initialTraverseDependencies(graph, options) {
  const delta = {
    added: /* @__PURE__ */ new Set(),
    modified: /* @__PURE__ */ new Set(),
    deleted: /* @__PURE__ */ new Set(),
    earlyInverseDependencies: /* @__PURE__ */ new Map()
  };
  const internalOptions = getInternalOptions(options);
  invariant(graph.dependencies.size === 0, "initialTraverseDependencies called on nonempty graph");
  invariant(graph.importBundleNames.size === 0, "initialTraverseDependencies called on nonempty graph");
  graph.privateState.gc.color.clear();
  graph.privateState.gc.possibleCycleRoots.clear();
  graph.privateState.gc.importBundleRefs.clear();
  for (const path2 of graph.entryPoints) {
    graph.privateState.gc.color.set(path2, "black");
  }
  await Promise.all(
    [...graph.entryPoints].map((path2) => traverseDependenciesForSingleFile(path2, graph, delta, internalOptions))
  );
  reorderGraph(graph, {
    shallow: options.shallow
  });
  return {
    added: graph.dependencies,
    modified: /* @__PURE__ */ new Map(),
    deleted: /* @__PURE__ */ new Set()
  };
}
async function traverseDependenciesForSingleFile(path2, graph, delta, options) {
  options.onDependencyAdd();
  await processModule(path2, graph, delta, options);
  options.onDependencyAdded();
}
async function processModule(path2, graph, delta, options) {
  const resolvedContext = graph.privateState.resolvedContexts.get(path2);
  const result = await options.transform(path2, resolvedContext);
  const currentDependencies = resolveDependencies(graph, path2, result.dependencies, options);
  const previousModule = graph.dependencies.get(path2) || {
    inverseDependencies: delta.earlyInverseDependencies.get(path2) || new import_CountingSet.default(),
    path: path2
  };
  const previousDependencies = previousModule.dependencies || /* @__PURE__ */ new Map();
  const module2 = {
    ...previousModule,
    dependencies: new Map(previousDependencies),
    getSource: result.getSource,
    output: result.output
  };
  graph.dependencies.set(module2.path, module2);
  for (const [key, prevDependency] of previousDependencies) {
    const curDependency = currentDependencies.get(key);
    if (!curDependency || !dependenciesEqual(prevDependency, curDependency, options)) {
      removeDependency(module2, key, prevDependency, graph, delta, options);
    }
  }
  const promises = [];
  for (const [key, curDependency] of currentDependencies) {
    const prevDependency = previousDependencies.get(key);
    if (!prevDependency || !dependenciesEqual(prevDependency, curDependency, options)) {
      promises.push(addDependency(module2, key, curDependency, graph, delta, options));
    }
  }
  await Promise.all(promises);
  invariant(module2.dependencies.size === currentDependencies.size, "Failed to add the correct dependencies");
  module2.dependencies = currentDependencies;
  return module2;
}
function dependenciesEqual(a, b, options) {
  return a === b || a.absolutePath === b.absolutePath && (!options.experimentalImportBundleSupport || a.data.data.asyncType === b.data.data.asyncType) && contextParamsEqual(a.data.data.contextParams, b.data.data.contextParams);
}
function contextParamsEqual(a, b) {
  return a === b || a == null && b == null || a != null && b != null && a.recursive === b.recursive && a.filter.pattern === b.filter.pattern && a.filter.flags === b.filter.flags && a.mode === b.mode;
}
async function addDependency(parentModule, key, dependency, graph, delta, options) {
  const path2 = dependency.absolutePath;
  let module2 = graph.dependencies.get(path2);
  if (options.shallow) {
  } else if (options.experimentalImportBundleSupport && dependency.data.data.asyncType != null) {
    incrementImportBundleReference(dependency, graph);
  } else {
    if (!module2) {
      const earlyInverseDependencies = delta.earlyInverseDependencies.get(path2);
      if (earlyInverseDependencies) {
        earlyInverseDependencies.add(parentModule.path);
      } else {
        if (delta.deleted.has(path2)) {
          delta.deleted.delete(path2);
        } else {
          delta.added.add(path2);
          delta.modified.delete(path2);
        }
        delta.earlyInverseDependencies.set(path2, new import_CountingSet.default());
        options.onDependencyAdd();
        module2 = await processModule(path2, graph, delta, options);
        options.onDependencyAdded();
        graph.dependencies.set(module2.path, module2);
      }
    }
    if (module2) {
      module2.inverseDependencies.add(parentModule.path);
      markModuleInUse(module2, graph);
    }
  }
  parentModule.dependencies.set(key, dependency);
}
function removeDependency(parentModule, key, dependency, graph, delta, options) {
  parentModule.dependencies.delete(key);
  const { absolutePath } = dependency;
  if (options.experimentalImportBundleSupport && dependency.data.data.asyncType != null) {
    decrementImportBundleReference(dependency, graph);
  }
  const module2 = graph.dependencies.get(absolutePath);
  if (!module2) {
    return;
  }
  module2.inverseDependencies.delete(parentModule.path);
  if (module2.inverseDependencies.size > 0 || graph.entryPoints.has(absolutePath)) {
    markAsPossibleCycleRoot(module2, graph);
  } else {
    releaseModule(module2, graph, delta, options);
  }
}
function markModifiedContextModules(graph, filePath, modifiedPaths) {
  for (const [absolutePath, context] of graph.privateState.resolvedContexts) {
    if (!modifiedPaths.has(absolutePath) && (0, import_contextModule.fileMatchesContext)(filePath, context)) {
      modifiedPaths.add(absolutePath);
    }
  }
}
function resolveDependencies(graph, parentPath, dependencies, options) {
  const maybeResolvedDeps = /* @__PURE__ */ new Map();
  for (const dep of dependencies) {
    let resolvedDep;
    const { contextParams } = dep.data;
    if (contextParams) {
      const from = path.join(parentPath, "..", dep.name);
      const absolutePath = (0, import_contextModule.deriveAbsolutePathFromContext)(from, contextParams);
      const resolvedContext = {
        from,
        mode: contextParams.mode,
        recursive: contextParams.recursive,
        filter: new RegExp(contextParams.filter.pattern, contextParams.filter.flags)
      };
      graph.privateState.resolvedContexts.set(absolutePath, resolvedContext);
      resolvedDep = {
        absolutePath,
        data: dep
      };
    } else {
      try {
        resolvedDep = {
          absolutePath: options.resolve(parentPath, dep.name),
          data: dep
        };
        graph.privateState.resolvedContexts.delete(resolvedDep.absolutePath);
      } catch (error) {
        if (dep.data.isOptional !== true) {
          throw error;
        }
      }
    }
    const key = dep.data.key;
    if (maybeResolvedDeps.has(key)) {
      throw new Error(`resolveDependencies: Found duplicate dependency key '${key}' in ${parentPath}`);
    }
    maybeResolvedDeps.set(key, resolvedDep);
  }
  const resolvedDeps = /* @__PURE__ */ new Map();
  for (const [key, resolvedDep] of maybeResolvedDeps) {
    if (resolvedDep) {
      resolvedDeps.set(key, resolvedDep);
    }
  }
  return resolvedDeps;
}
function reorderGraph(graph, options) {
  const orderedDependencies = /* @__PURE__ */ new Map();
  graph.entryPoints.forEach((entryPoint) => {
    const mainModule = graph.dependencies.get(entryPoint);
    if (!mainModule) {
      throw new ReferenceError("Module not registered in graph: " + entryPoint);
    }
    reorderDependencies(graph, mainModule, orderedDependencies, options);
  });
  graph.dependencies = orderedDependencies;
}
function reorderDependencies(graph, module2, orderedDependencies, options) {
  if (module2.path) {
    if (orderedDependencies.has(module2.path)) {
      return;
    }
    orderedDependencies.set(module2.path, module2);
  }
  module2.dependencies.forEach((dependency) => {
    const path2 = dependency.absolutePath;
    const childModule = graph.dependencies.get(path2);
    if (!childModule) {
      if (dependency.data.data.asyncType != null || options.shallow) {
        return;
      } else {
        throw new ReferenceError("Module not registered in graph: " + path2);
      }
    }
    reorderDependencies(graph, childModule, orderedDependencies, options);
  });
}
function incrementImportBundleReference(dependency, graph) {
  const { absolutePath } = dependency;
  graph.privateState.gc.importBundleRefs.set(
    absolutePath,
    (graph.privateState.gc.importBundleRefs.get(absolutePath) ?? 0) + 1
  );
  graph.importBundleNames.add(absolutePath);
}
function decrementImportBundleReference(dependency, graph) {
  const { absolutePath } = dependency;
  const prevRefCount = nullthrows(graph.privateState.gc.importBundleRefs.get(absolutePath));
  invariant(prevRefCount > 0, "experimentalImportBundleSupport: import bundle refcount not valid");
  graph.privateState.gc.importBundleRefs.set(absolutePath, prevRefCount - 1);
  if (prevRefCount === 1) {
    graph.privateState.gc.importBundleRefs.delete(absolutePath);
    graph.importBundleNames.delete(absolutePath);
  }
}
function markModuleInUse(module2, graph) {
  graph.privateState.gc.color.set(module2.path, "black");
}
function releaseModule(module2, graph, delta, options) {
  for (const [key, dependency] of module2.dependencies) {
    removeDependency(module2, key, dependency, graph, delta, options);
  }
  graph.privateState.gc.color.set(module2.path, "black");
  if (!graph.privateState.gc.possibleCycleRoots.has(module2.path)) {
    freeModule(module2, graph, delta);
  }
}
function freeModule(module2, graph, delta) {
  if (delta.added.has(module2.path)) {
    delta.added.delete(module2.path);
  } else {
    delta.deleted.add(module2.path);
    delta.modified.delete(module2.path);
  }
  graph.dependencies.delete(module2.path);
  delta.earlyInverseDependencies.delete(module2.path);
  graph.privateState.gc.possibleCycleRoots.delete(module2.path);
  graph.privateState.gc.color.delete(module2.path);
  graph.privateState.resolvedContexts.delete(module2.path);
}
function markAsPossibleCycleRoot(module2, graph) {
  if (nullthrows(graph.privateState.gc.color.get(module2.path)) !== "purple") {
    graph.privateState.gc.color.set(module2.path, "purple");
    graph.privateState.gc.possibleCycleRoots.add(module2.path);
  }
}
function collectCycles(graph, delta) {
  for (const path2 of graph.privateState.gc.possibleCycleRoots) {
    const module2 = nullthrows(graph.dependencies.get(path2));
    const color = nullthrows(graph.privateState.gc.color.get(path2));
    if (color === "purple") {
      markGray(module2, graph);
    } else {
      graph.privateState.gc.possibleCycleRoots.delete(path2);
      if (color === "black" && module2.inverseDependencies.size === 0 && !graph.entryPoints.has(path2)) {
        freeModule(module2, graph, delta);
      }
    }
  }
  for (const path2 of graph.privateState.gc.possibleCycleRoots) {
    const module2 = nullthrows(graph.dependencies.get(path2));
    scan(module2, graph);
  }
  for (const path2 of graph.privateState.gc.possibleCycleRoots) {
    graph.privateState.gc.possibleCycleRoots.delete(path2);
    const module2 = nullthrows(graph.dependencies.get(path2));
    collectWhite(module2, graph, delta);
  }
}
function markGray(module2, graph) {
  const color = nullthrows(graph.privateState.gc.color.get(module2.path));
  if (color !== "gray") {
    graph.privateState.gc.color.set(module2.path, "gray");
    for (const dependency of module2.dependencies.values()) {
      const childModule = nullthrows(graph.dependencies.get(dependency.absolutePath));
      childModule.inverseDependencies.delete(module2.path);
      markGray(childModule, graph);
    }
  }
}
function scan(module2, graph) {
  const color = nullthrows(graph.privateState.gc.color.get(module2.path));
  if (color === "gray") {
    if (module2.inverseDependencies.size > 0 || graph.entryPoints.has(module2.path)) {
      scanBlack(module2, graph);
    } else {
      graph.privateState.gc.color.set(module2.path, "white");
      for (const dependency of module2.dependencies.values()) {
        const childModule = nullthrows(graph.dependencies.get(dependency.absolutePath));
        scan(childModule, graph);
      }
    }
  }
}
function scanBlack(module2, graph) {
  graph.privateState.gc.color.set(module2.path, "black");
  for (const dependency of module2.dependencies.values()) {
    const childModule = nullthrows(graph.dependencies.get(dependency.absolutePath));
    childModule.inverseDependencies.add(module2.path);
    const childColor = nullthrows(graph.privateState.gc.color.get(childModule.path));
    if (childColor !== "black") {
      scanBlack(childModule, graph);
    }
  }
}
function collectWhite(module2, graph, delta) {
  const color = nullthrows(graph.privateState.gc.color.get(module2.path));
  if (color === "white" && !graph.privateState.gc.possibleCycleRoots.has(module2.path)) {
    graph.privateState.gc.color.set(module2.path, "black");
    for (const dependency of module2.dependencies.values()) {
      const childModule = graph.dependencies.get(dependency.absolutePath);
      if (childModule) {
        collectWhite(childModule, graph, delta);
      }
    }
    freeModule(module2, graph, delta);
  }
}
module.exports = {
  createGraph,
  initialTraverseDependencies,
  traverseDependencies,
  reorderGraph,
  markModifiedContextModules
};
