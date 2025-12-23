"use strict";
const buildSourcemapWithMetadata = require("../../shared/output/RamBundle/buildSourcemapWithMetadata.js");
const MAGIC_UNBUNDLE_NUMBER = require("../../shared/output/RamBundle/magic-number");
const { getModuleCodeAndMap, partition, toModuleTransport } = require("./util");
const path = require("path");
const MAGIC_UNBUNDLE_FILENAME = "UNBUNDLE";
const JS_MODULES = "js-modules";
function asMultipleFilesRamBundle({
  dependencyMapReservedName,
  filename,
  globalPrefix,
  idsForPath,
  modules,
  requireCalls,
  preloadedModules
}) {
  const idForPath = (x) => idsForPath(x).moduleId;
  const [startup, deferred] = partition(modules, preloadedModules);
  const startupModules = [...startup, ...requireCalls];
  const deferredModules = deferred.map(
    (m) => toModuleTransport(m, idsForPath, { dependencyMapReservedName, globalPrefix })
  );
  const magicFileContents = Buffer.alloc(4);
  const code = startupModules.map(
    (m) => getModuleCodeAndMap(m, idForPath, {
      dependencyMapReservedName,
      enableIDInlining: true,
      globalPrefix
    }).moduleCode
  ).join("\n");
  const extraFiles = /* @__PURE__ */ new Map();
  deferredModules.forEach((deferredModule) => {
    extraFiles.set(path.join(JS_MODULES, deferredModule.id + ".js"), deferredModule.code);
  });
  magicFileContents.writeUInt32LE(MAGIC_UNBUNDLE_NUMBER, 0);
  extraFiles.set(path.join(JS_MODULES, MAGIC_UNBUNDLE_FILENAME), magicFileContents);
  const map = buildSourcemapWithMetadata({
    fixWrapperOffset: false,
    lazyModules: deferredModules,
    moduleGroups: null,
    startupModules: startupModules.map(
      (m) => toModuleTransport(m, idsForPath, {
        dependencyMapReservedName,
        globalPrefix
      })
    )
  });
  return { code, extraFiles, map };
}
function createBuilder(preloadedModules, ramGroupHeads) {
  return (x) => asMultipleFilesRamBundle({ ...x, preloadedModules, ramGroupHeads });
}
exports.createBuilder = createBuilder;
