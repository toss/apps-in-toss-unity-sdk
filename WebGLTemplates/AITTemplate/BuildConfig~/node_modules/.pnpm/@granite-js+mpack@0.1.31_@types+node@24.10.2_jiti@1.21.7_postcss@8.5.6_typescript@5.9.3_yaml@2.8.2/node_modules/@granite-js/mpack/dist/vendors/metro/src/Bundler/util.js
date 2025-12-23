"use strict";
const babylon = require("@babel/parser");
const template = require("@babel/template").default;
const babelTypes = require("@babel/types");
const nullthrows = require("nullthrows");
const assetPropertyBlockList = /* @__PURE__ */ new Set(["files", "fileSystemLocation", "path"]);
function generateAssetCodeFileAst(assetRegistryPath, assetDescriptor) {
  const properDescriptor = filterObject(assetDescriptor, assetPropertyBlockList);
  const descriptorAst = babylon.parseExpression(JSON.stringify(properDescriptor));
  const t = babelTypes;
  const buildRequire = template.statement(`
    module.exports = require(ASSET_REGISTRY_PATH).registerAsset(DESCRIPTOR_AST)
  `);
  return t.file(
    t.program([
      buildRequire({
        ASSET_REGISTRY_PATH: t.stringLiteral(assetRegistryPath),
        DESCRIPTOR_AST: descriptorAst
      })
    ])
  );
}
function generateRemoteAssetCodeFileAst(assetUtilsPath, assetDescriptor, remoteServer, remoteFileMap) {
  const t = babelTypes;
  const file = remoteFileMap[assetDescriptor.fileSystemLocation];
  const descriptor = file && file[assetDescriptor.name];
  const data = {};
  if (!descriptor) {
    return null;
  }
  for (const scale in descriptor) {
    data[+scale] = descriptor[+scale].handle;
  }
  const astData = babylon.parseExpression(JSON.stringify(data));
  const URI = t.stringLiteral(remoteServer);
  const WIDTH = t.numericLiteral(nullthrows(assetDescriptor.width));
  const HEIGHT = t.numericLiteral(nullthrows(assetDescriptor.height));
  const buildRequire = template.program(`
    const {pickScale, getUrlCacheBreaker}= require(ASSET_UTILS_PATH);
    module.exports = {
      "width": WIDTH,
      "height": HEIGHT,
      "uri": URI + OBJECT_AST[pickScale(SCALE_ARRAY)] + getUrlCacheBreaker()
    };
  `);
  return t.file(
    buildRequire({
      WIDTH,
      HEIGHT,
      URI,
      OBJECT_AST: astData,
      ASSET_UTILS_PATH: t.stringLiteral(assetUtilsPath),
      SCALE_ARRAY: t.arrayExpression(
        Object.keys(descriptor).map(Number).sort((a, b) => a - b).map((scale) => t.numericLiteral(scale))
      )
    })
  );
}
function isAssetTypeAnImage(type) {
  return ["png", "jpg", "jpeg", "bmp", "gif", "webp", "psd", "svg", "tiff"].indexOf(type) !== -1;
}
function filterObject(object, blockList) {
  const copied = { ...object };
  for (const key of blockList) {
    delete copied[key];
  }
  return copied;
}
function createRamBundleGroups(ramGroups, groupableModules, subtree) {
  const byPath = /* @__PURE__ */ new Map();
  const byId = /* @__PURE__ */ new Map();
  groupableModules.forEach((m) => {
    byPath.set(m.sourcePath, m);
    byId.set(m.id, m.sourcePath);
  });
  const result = new Map(
    ramGroups.map((modulePath) => {
      const root = byPath.get(modulePath);
      if (root == null) {
        throw Error(`Group root ${modulePath} is not part of the bundle`);
      }
      return [
        root.id,
        // `subtree` yields the IDs of all transitive dependencies of a module
        new Set(subtree(root, byPath))
      ];
    })
  );
  if (ramGroups.length > 1) {
    const all = new ArrayMap();
    for (const [parent, children] of result) {
      for (const module2 of children) {
        all.get(module2).push(parent);
      }
    }
    const doubles = filter(all, ([, parents]) => parents.length > 1);
    for (const [moduleId, parents] of doubles) {
      const parentNames = parents.map(byId.get, byId);
      const lastName = parentNames.pop();
      throw new Error(
        `Module ${byId.get(moduleId) || moduleId} belongs to groups ${parentNames.join(", ")}, and ${String(
          lastName
        )}. Ensure that each module is only part of one group.`
      );
    }
  }
  return result;
}
function* filter(iterator, predicate) {
  for (const value of iterator) {
    if (predicate(value)) {
      yield value;
    }
  }
}
class ArrayMap extends Map {
  get(key) {
    let array = super.get(key);
    if (!array) {
      array = [];
      this.set(key, array);
    }
    return array;
  }
}
module.exports = {
  createRamBundleGroups,
  generateAssetCodeFileAst,
  generateRemoteAssetCodeFileAst,
  isAssetTypeAnImage
};
