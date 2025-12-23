"use strict";
var __defProp = Object.defineProperty;
var __getOwnPropDesc = Object.getOwnPropertyDescriptor;
var __getOwnPropNames = Object.getOwnPropertyNames;
var __hasOwnProp = Object.prototype.hasOwnProperty;
var __export = (target, all) => {
  for (var name in all)
    __defProp(target, name, { get: all[name], enumerable: true });
};
var __copyProps = (to, from, except, desc) => {
  if (from && typeof from === "object" || typeof from === "function") {
    for (let key of __getOwnPropNames(from))
      if (!__hasOwnProp.call(to, key) && key !== except)
        __defProp(to, key, { get: () => from[key], enumerable: !(desc = __getOwnPropDesc(from, key)) || desc.enumerable });
  }
  return to;
};
var __toCommonJS = (mod) => __copyProps(__defProp({}, "__esModule", { value: true }), mod);
var assetRegistry_exports = {};
__export(assetRegistry_exports, {
  getAssetByID: () => noop,
  registerAsset: () => registerAsset
});
module.exports = __toCommonJS(assetRegistry_exports);
const IGNORE_PATH_PATTERN = [/react-native\/Libraries\/LogBox\/UI/, /@react-navigation\/elements/];
const assets = /* @__PURE__ */ new Set();
function registerAsset(asset) {
  const key = getUniqueKey(asset);
  if (assets.has(key)) {
    return;
  }
  assets.add(key);
  warnUnableToUseLocalResources(asset);
}
function warnUnableToUseLocalResources(asset) {
  const localPath = asset.httpServerLocation.replace(/^\/assets\//, "");
  const fileName = `${asset.name}.${asset.type}`;
  const shouldIgnore = IGNORE_PATH_PATTERN.some((pattern) => pattern.test(localPath));
  if (__DEV__ && !shouldIgnore) {
    console.warn(
      [
        "(DEV) \uB85C\uCEEC \uB9AC\uC18C\uC2A4\uB294 \uC9C0\uC6D0\uB418\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4. URI \uBC29\uC2DD\uC758 \uC6D0\uACA9 \uB9AC\uC18C\uC2A4\uB97C \uC0AC\uC6A9\uD574\uC8FC\uC138\uC694.",
        `Resource: ${localPath}/${fileName}`
      ].join("\n")
    );
  }
}
function getUniqueKey(asset) {
  return `${asset.name}#${asset.hash}`;
}
function noop() {
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  getAssetByID,
  registerAsset
});
