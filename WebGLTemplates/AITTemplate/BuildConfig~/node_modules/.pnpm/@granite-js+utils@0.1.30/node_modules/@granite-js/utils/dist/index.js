// src/getPackageRoot.ts
import fs from "fs";
import path from "path";
function getPackageRoot() {
  let cwd = process.cwd();
  const root = path.parse(cwd).root;
  while (cwd !== root) {
    if (fs.existsSync(path.join(cwd, "package.json"))) {
      return cwd;
    }
    cwd = path.dirname(cwd);
  }
  return cwd;
}

// src/readZipContent.ts
import yauzl from "yauzl";
function readZipContent(zipPath, fileName) {
  return new Promise((resolve, reject) => {
    yauzl.open(zipPath, { lazyEntries: true }, (error, zipFile) => {
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
    yauzl.open(zipPath, { lazyEntries: true }, (error, zipFile) => {
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
import fs2 from "fs";
import path2 from "path";
function getLocalTempDirectoryPath(rootDir) {
  return path2.resolve(rootDir, ".granite");
}
function prepareLocalDirectory(rootDir) {
  const localDir = getLocalTempDirectoryPath(rootDir);
  if (!fs2.existsSync(localDir)) {
    fs2.mkdirSync(localDir, { recursive: true });
  }
  return localDir;
}
export {
  ensureSafetyInvokeSync,
  getLocalTempDirectoryPath,
  getPackageRoot,
  prepareLocalDirectory,
  readZipContent,
  readZipEntries
};
