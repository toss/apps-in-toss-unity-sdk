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
var requireContextPlugin_exports = {};
__export(requireContextPlugin_exports, {
  requireContextPlugin: () => requireContextPlugin
});
module.exports = __toCommonJS(requireContextPlugin_exports);
var import_fs = __toESM(require("fs"));
var import_path = __toESM(require("path"));
var import_scripts = require("./scripts");
var import_constants = require("../../../constants");
var import_esbuildUtils = require("../../../utils/esbuildUtils");
async function getFilePaths(rootDir) {
  const filenames = [];
  const rootFilenames = await import_fs.default.readdirSync(rootDir);
  for await (const filename of rootFilenames) {
    const filePath = import_path.default.join(rootDir, filename);
    if (import_fs.default.lstatSync(filePath).isDirectory()) {
      filenames.push(...await getFilePaths(filePath));
    } else {
      filenames.push(filePath);
    }
  }
  return filenames;
}
function requireContextPlugin() {
  return {
    name: "require-context-plugin",
    setup(build) {
      build.onLoad({ filter: /require\.context\.tsx?$/ }, async (args) => {
        const content = await import_fs.default.promises.readFile(args.path, "utf8");
        return {
          loader: args.path.endsWith(".tsx") ? "tsx" : "ts",
          contents: (0, import_scripts.toRequireContextExportScript)(content)
        };
      });
      build.onResolve({ filter: new RegExp(`^${import_constants.REQUIRE_CONTEXT_PROTOCOL}.*`) }, (args) => {
        return {
          namespace: "require-context",
          path: args.path.slice(import_constants.REQUIRE_CONTEXT_PROTOCOL.length),
          pluginData: {
            importer: args.resolveDir
          }
        };
      });
      build.onLoad({ namespace: "require-context", filter: /.*/ }, async (args) => {
        const importer = args.pluginData?.importer;
        if (importer == null) {
          throw new Error(`importer\uAC00 \uC8FC\uC5B4\uC838\uC57C \uD569\uB2C8\uB2E4.`);
        }
        const targetDir = import_path.default.resolve(importer, args.path);
        const basePath = import_path.default.join(importer, args.path);
        const filePaths = await getFilePaths(targetDir);
        const requireContextModules = filePaths.map((filePath, index) => {
          const pagePath = import_path.default.relative(basePath, filePath);
          const normalizedPagePath = (0, import_esbuildUtils.normalizePath)(pagePath);
          const normalizedFilePath = (0, import_esbuildUtils.normalizePath)(filePath);
          return {
            moduleIndex: index,
            relativePath: normalizedPagePath.startsWith(".") ? normalizedPagePath : `./${normalizedPagePath}`,
            absolutePath: normalizedFilePath
          };
        });
        return {
          contents: (0, import_scripts.getRequireContextScript)(requireContextModules),
          resolveDir: importer
        };
      });
    }
  };
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  requireContextPlugin
});
