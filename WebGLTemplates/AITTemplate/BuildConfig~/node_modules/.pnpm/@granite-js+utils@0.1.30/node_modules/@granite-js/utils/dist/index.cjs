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

// src/index.ts
var index_exports = {};
__export(index_exports, {
  ensureSafetyInvokeSync: () => ensureSafetyInvokeSync,
  getLocalTempDirectoryPath: () => getLocalTempDirectoryPath,
  getPackageRoot: () => getPackageRoot,
  prepareLocalDirectory: () => prepareLocalDirectory,
  readZipContent: () => readZipContent,
  readZipEntries: () => readZipEntries
});
module.exports = __toCommonJS(index_exports);

// src/getPackageRoot.ts
var import_fs = __toESM(require("fs"), 1);
var import_path = __toESM(require("path"), 1);
function getPackageRoot() {
  let cwd = process.cwd();
  const root = import_path.default.parse(cwd).root;
  while (cwd !== root) {
    if (import_fs.default.existsSync(import_path.default.join(cwd, "package.json"))) {
      return cwd;
    }
    cwd = import_path.default.dirname(cwd);
  }
  return cwd;
}

// src/readZipContent.ts
var import_yauzl = __toESM(require("yauzl"), 1);
function readZipContent(zipPath, fileName) {
  return new Promise((resolve, reject) => {
    import_yauzl.default.open(zipPath, { lazyEntries: true }, (error, zipFile) => {
      if (error) {
        reject(error);
        return;
      }
      zipFile.on("entry", (entry) => {
        if (entry.fileName === fileName) {
          zipFile.openReadStream(entry, (error2, readStream) => {
            if (error2) {
              throw error2;
            }
            let fileData = "";
            readStream.on("data", (chunk) => fileData += chunk.toString("utf8")).on("end", () => {
              zipFile.close();
              resolve(fileData);
            });
          });
        } else {
          zipFile.readEntry();
        }
      }).on("end", () => {
        zipFile.close();
        reject(new Error(`'${fileName}' not found in zip file`));
      }).on("error", (error2) => {
        zipFile.close();
        reject(error2);
      });
      zipFile.readEntry();
    });
  });
}
function readZipEntries(zipPath) {
  return new Promise((resolve, reject) => {
    import_yauzl.default.open(zipPath, { lazyEntries: true }, (error, zipFile) => {
      if (error || !zipFile) {
        reject(error || new Error("Failed to open zip file"));
        return;
      }
      const contents = {};
      let processing = 0;
      let finished = false;
      const maybeFinish = () => {
        if (finished && processing === 0) {
          zipFile.close();
          resolve(contents);
        }
      };
      zipFile.readEntry();
      zipFile.on("entry", (entry) => {
        if (/\/$/.test(entry.fileName)) {
          zipFile.readEntry();
          return;
        }
        processing++;
        zipFile.openReadStream(entry, (error2, readStream) => {
          if (error2 || !readStream) {
            zipFile.close();
            reject(error2 || new Error("Failed to open read stream"));
            return;
          }
          const chunks = [];
          readStream.on("data", (chunk) => chunks.push(chunk)).on("end", () => {
            contents[entry.fileName] = Buffer.concat(chunks).toString("utf8");
            processing--;
            maybeFinish();
            zipFile.readEntry();
          }).on("error", (error3) => {
            zipFile.close();
            reject(error3);
          });
        });
      });
      zipFile.on("end", () => {
        finished = true;
        maybeFinish();
      });
      zipFile.on("error", (error2) => {
        zipFile.close();
        reject(error2);
      });
    });
  });
}

// src/ensureSafetyInvoke.ts
function ensureSafetyInvokeSync(fn) {
  return (...args) => {
    try {
      return fn.call(null, ...args);
    } catch (error) {
      console.error(error);
      return null;
    }
  };
}

// src/localTempDirectory.ts
var import_fs2 = __toESM(require("fs"), 1);
var import_path2 = __toESM(require("path"), 1);
function getLocalTempDirectoryPath(rootDir) {
  return import_path2.default.resolve(rootDir, ".granite");
}
function prepareLocalDirectory(rootDir) {
  const localDir = getLocalTempDirectoryPath(rootDir);
  if (!import_fs2.default.existsSync(localDir)) {
    import_fs2.default.mkdirSync(localDir, { recursive: true });
  }
  return localDir;
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  ensureSafetyInvokeSync,
  getLocalTempDirectoryPath,
  getPackageRoot,
  prepareLocalDirectory,
  readZipContent,
  readZipEntries
});
