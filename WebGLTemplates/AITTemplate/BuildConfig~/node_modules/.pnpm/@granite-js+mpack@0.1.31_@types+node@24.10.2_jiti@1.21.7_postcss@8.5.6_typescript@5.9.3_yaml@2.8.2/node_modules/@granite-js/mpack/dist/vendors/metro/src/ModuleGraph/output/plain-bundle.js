"use strict";
const meta = require("../../shared/output/meta");
const { concat, getModuleCodeAndMap } = require("./util");
const { BundleBuilder } = require("metro-source-map");
function asPlainBundle({
  dependencyMapReservedName,
  filename,
  globalPrefix,
  idsForPath,
  modules,
  requireCalls,
  sourceMapPath,
  enableIDInlining
}) {
  const builder = new BundleBuilder(filename);
  const modIdForPath = (x) => idsForPath(x).moduleId;
  for (const module2 of concat(modules, requireCalls)) {
    const { moduleCode, moduleMap } = getModuleCodeAndMap(module2, modIdForPath, {
      dependencyMapReservedName,
      enableIDInlining,
      globalPrefix
    });
    builder.append(moduleCode + "\n", moduleMap);
  }
  if (sourceMapPath) {
    builder.append(`//# sourceMappingURL=${sourceMapPath}`);
  }
  const code = builder.getCode();
  const map = builder.getMap();
  return {
    code,
    extraFiles: [[`${filename}.meta`, meta(code)]],
    map
  };
}
module.exports = asPlainBundle;
