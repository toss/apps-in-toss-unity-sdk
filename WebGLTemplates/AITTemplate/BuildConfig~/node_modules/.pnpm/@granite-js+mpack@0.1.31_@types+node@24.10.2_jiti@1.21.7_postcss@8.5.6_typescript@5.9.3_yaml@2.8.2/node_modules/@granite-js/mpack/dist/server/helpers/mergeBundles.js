"use strict";
var __create = Object.create;
var __defProp = Object.defineProperty;
var __getOwnPropDesc = Object.getOwnPropertyDescriptor;
var __getOwnPropNames = Object.getOwnPropertyNames;
var __getProtoOf = Object.getPrototypeOf;
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
var __toESM = (mod, isNodeMode, target) => (target = mod != null ? __create(__getProtoOf(mod)) : {}, __copyProps(
  // If the importer is in node compatibility mode or this is not an ESM
  // file that has been converted to a CommonJS file using a Babel-
  // compatible transform (i.e. "__esModule" has not been set), then set
  // "default" to the CommonJS "module.exports" for node compatibility.
  isNodeMode || !mod || !mod.__esModule ? __defProp(target, "default", { value: mod, enumerable: true }) : target,
  mod
));
var __toCommonJS = (mod) => __copyProps(__defProp({}, "__esModule", { value: true }), mod);
var mergeBundles_exports = {};
__export(mergeBundles_exports, {
  mergeBundles: () => mergeBundles
});
module.exports = __toCommonJS(mergeBundles_exports);
var esbuild = __toESM(require("esbuild"));
var import_constants = require("../../constants");
var import_getBundleName = require("../../utils/getBundleName");
var import_getSourcemapName = require("../../utils/getSourcemapName");
var import_constants2 = require("../constants");
const INTERNAL_SOURCE = "mpack:internal";
const IMPORT_SOURCE = "mpack:remote";
async function mergeBundles({
  platform,
  hostBundleContent,
  remoteBundleContent
}) {
  const bundleName = (0, import_getBundleName.getBundleName)(import_constants2.DEV_SERVER_BUNDLE_NAME);
  const sourcemapName = (0, import_getSourcemapName.getSourcemapName)(bundleName);
  const result = await esbuild.build({
    logLevel: "silent",
    stdin: {
      contents: [`require('${INTERNAL_SOURCE}');`, hostBundleContent].join("\n"),
      loader: "js"
    },
    outfile: bundleName,
    bundle: true,
    write: false,
    minify: false,
    treeShaking: false,
    sourcemap: "external",
    sourcesContent: true,
    define: {
      global: "window"
    },
    footer: {
      /**
       * 소스맵 옵션을 `external`로 지정하여 번들 코드 하단에 `#sourceMappingURL=path` 을 붙이지 않도록 하고,
       * `footer` 옵션을 통해 개발 서버의 소스맵 주소(상대경로)로 매핑되도록 직접 추가함.
       *
       * - eg. `/index.bundle.map?platform=<platform>&dev=true`
       *
       * @see {@link https://esbuild.github.io/api/#sourcemap}
       * @see {@link https://esbuild.github.io/api/#footer}
       */
      js: `//# sourceMappingURL=/${sourcemapName}?${new URLSearchParams({
        platform,
        dev: JSON.stringify(true)
      }).toString()}`
    },
    plugins: [
      {
        name: "remote-bundle-loader",
        setup(build) {
          build.onResolve({ filter: new RegExp(`^${INTERNAL_SOURCE}$`) }, () => ({
            path: "dev-server-runtime",
            namespace: "DEV_SERVER_RUNTIME"
          }));
          build.onResolve({ filter: new RegExp(`^${IMPORT_SOURCE}$`) }, () => ({
            path: "remote-bundle",
            namespace: "REMOTE_BUNDLE"
          }));
          build.onLoad({ filter: /.*/, namespace: "DEV_SERVER_RUNTIME" }, () => ({
            contents: `
            (function(global) {
              global.${import_constants.INTERNAL_NAMESPACE_IDENTIFIER} = {};
              global.${import_constants.INTERNAL_NAMESPACE_IDENTIFIER}.${import_constants.INTERNAL_LOAD_REMOTE_IDENTIFIER} = function INTERNAL__loadRemote() {
                require('${IMPORT_SOURCE}');
                return Promise.resolve();
              }
            })(
              typeof globalThis !== 'undefined'
                ? globalThis
                : typeof global !== 'undefined'
                  ? global
                  : typeof window !== 'undefined'
                    ? window
                    : this
            );
            `,
            loader: "js"
          }));
          build.onLoad({ filter: /.*/, namespace: "REMOTE_BUNDLE" }, () => ({
            contents: remoteBundleContent,
            loader: "js"
          }));
        }
      }
    ]
  });
  const sourceFile = result.outputFiles.find((file) => file.path.endsWith(bundleName));
  const sourcemapFile = result.outputFiles.find((file) => file.path.endsWith(sourcemapName));
  if (sourceFile && sourcemapFile) {
    return { source: sourceFile, sourcemap: sourcemapFile };
  }
  throw new Error("failed to merge bundle");
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  mergeBundles
});
