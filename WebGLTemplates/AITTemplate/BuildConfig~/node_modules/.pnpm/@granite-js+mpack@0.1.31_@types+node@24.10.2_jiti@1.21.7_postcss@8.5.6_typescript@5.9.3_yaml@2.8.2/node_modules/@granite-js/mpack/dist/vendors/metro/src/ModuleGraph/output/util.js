"use strict";
const virtualModule = require("../module").virtual;
const generate = require("../worker/generate");
const mergeSourceMaps = require("../worker/mergeSourceMaps");
const reverseDependencyMapReferences = require("./reverse-dependency-map-references");
const { parseSync, transformFromAstSync } = require("@babel/core");
const { passthroughSyntaxPlugins } = require("metro-react-native-babel-preset");
const { addParamsToDefineCall } = require("metro-transform-plugins");
const nullthrows = require("nullthrows");
function addModuleIdsToModuleWrapper(module2, idForPath) {
  const { dependencies, file } = module2;
  const { code } = file;
  const fileId = idForPath(file);
  const paramsToAdd = [fileId];
  if (dependencies.length) {
    paramsToAdd.push(dependencies.map(idForPath));
  }
  return addParamsToDefineCall(code, ...paramsToAdd);
}
exports.addModuleIdsToModuleWrapper = addModuleIdsToModuleWrapper;
function isTypeScriptSource(fileName) {
  return !!fileName && fileName.endsWith(".ts");
}
function isTSXSource(fileName) {
  return !!fileName && fileName.endsWith(".tsx");
}
function inlineModuleIds(module2, idForPath, { dependencyMapReservedName, globalPrefix, ignoreMissingDependencyMapReference = false, hermesParser = false }) {
  const { dependencies, file } = module2;
  const { code, map, path } = file;
  const fileId = idForPath(file);
  const dependencyIds = dependencies.map(idForPath);
  if (!dependencyIds.length) {
    return { fileId, moduleCode: code, moduleMap: map };
  }
  if (dependencyMapReservedName != null) {
    if (!code.includes(dependencyMapReservedName)) {
      if (ignoreMissingDependencyMapReference) {
        return { fileId, moduleCode: code, moduleMap: map };
      }
      throw new Error(
        `Module has dependencies but does not use the preconfigured dependency map name '${dependencyMapReservedName}': ${file.path}
This is an internal error in Metro.`
      );
    }
    const WS = "[	 ]*";
    const depMapReferenceRegex = new RegExp(
      escapeRegex(dependencyMapReservedName) + `${WS}\\[${WS}([0-9]+)${WS}\\]`,
      "g"
    );
    const inlinedCode = code.replace(depMapReferenceRegex, (match, depIndex) => {
      const idStr = dependencyIds[Number.parseInt(depIndex, 10)].toString();
      if (idStr.length > match.length) {
        throw new Error(
          `Module ID doesn't fit in available space; add ${idStr.length - match.length} more characters to 'dependencyMapReservedName'.`
        );
      }
      return idStr.padEnd(match.length);
    });
    return {
      fileId,
      moduleCode: inlinedCode,
      moduleMap: map
    };
  }
  const babelConfig = {
    ast: true,
    babelrc: false,
    browserslistConfigFile: false,
    code: false,
    configFile: false,
    plugins: [...passthroughSyntaxPlugins, [reverseDependencyMapReferences, { dependencyIds, globalPrefix }]]
  };
  const sourceAst = isTypeScriptSource(path) || isTSXSource(path) || !hermesParser ? parseSync(code, babelConfig) : require("hermes-parser").parse(code, {
    babel: true,
    // $FlowFixMe[prop-missing]
    sourceType: babelConfig.sourceType
  });
  const ast = nullthrows(transformFromAstSync(sourceAst, code, babelConfig).ast);
  const { code: generatedCode, map: generatedMap } = generate(ast, path, "");
  return {
    fileId,
    moduleCode: generatedCode,
    moduleMap: map && generatedMap && mergeSourceMaps(path, map, generatedMap)
  };
}
function inlineModuleIdsAndAddParamsToDefineCall(module2, idForPath, options) {
  const { fileId, moduleCode, moduleMap } = inlineModuleIds(module2, idForPath, options);
  return { moduleCode: addParamsToDefineCall(moduleCode, fileId), moduleMap };
}
exports.inlineModuleIds = inlineModuleIds;
exports.inlineModuleIdsAndAddParamsToDefineCall = inlineModuleIdsAndAddParamsToDefineCall;
function escapeRegex(str) {
  return str.replace(/([.?*+^$[\]\\(){}|-])/g, "\\$1");
}
function getModuleCodeAndMap(module2, idForPath, options) {
  const { file } = module2;
  let moduleCode, moduleMap;
  if (file.type !== "module") {
    moduleCode = file.code;
    moduleMap = file.map;
  } else if (!options.enableIDInlining) {
    moduleCode = addModuleIdsToModuleWrapper(module2, idForPath);
    moduleMap = file.map;
  } else {
    ({ moduleCode, moduleMap } = inlineModuleIdsAndAddParamsToDefineCall(module2, idForPath, {
      dependencyMapReservedName: options.dependencyMapReservedName,
      globalPrefix: options.globalPrefix
    }));
  }
  if (moduleMap && moduleMap.sources) {
    const x_facebook_sources = [];
    if (moduleMap.sources.length >= 1) {
      x_facebook_sources.push([module2.file.functionMap]);
    }
    moduleMap = { ...moduleMap, x_facebook_sources };
  }
  return { moduleCode, moduleMap };
}
exports.getModuleCodeAndMap = getModuleCodeAndMap;
exports.concat = function* concat(...iterables) {
  for (const it of iterables) {
    yield* it;
  }
};
exports.createIdForPathFn = () => {
  const seen = /* @__PURE__ */ new Map();
  let next = 0;
  return ({ path }) => {
    let id = seen.get(path);
    if (id == null) {
      id = next++;
      seen.set(path, id);
    }
    return id;
  };
};
exports.requireCallsTo = function* (modules, idForPath, getRunModuleStatement) {
  for (const module2 of modules) {
    const id = idForPath(module2.file);
    yield virtualModule(getRunModuleStatement(id), `/<generated>/require-${id}.js`);
  }
};
exports.partition = (modules, preloadedModules) => {
  const startup = [];
  const deferred = [];
  for (const module2 of modules) {
    (preloadedModules.has(module2.file.path) ? startup : deferred).push(module2);
  }
  return [startup, deferred];
};
function toModuleTransport(module2, idsForPath, { dependencyMapReservedName, globalPrefix }) {
  const { dependencies, file } = module2;
  const { moduleCode, moduleMap } = getModuleCodeAndMap(module2, (x) => idsForPath(x).moduleId, {
    dependencyMapReservedName,
    enableIDInlining: true,
    globalPrefix
  });
  return {
    code: moduleCode,
    dependencies,
    // ID is required but we provide an invalid one for "script"s.
    id: file.type === "module" ? nullthrows(idsForPath(file).localId) : -1,
    map: moduleMap,
    name: file.path,
    sourcePath: file.path
  };
}
exports.toModuleTransport = toModuleTransport;
