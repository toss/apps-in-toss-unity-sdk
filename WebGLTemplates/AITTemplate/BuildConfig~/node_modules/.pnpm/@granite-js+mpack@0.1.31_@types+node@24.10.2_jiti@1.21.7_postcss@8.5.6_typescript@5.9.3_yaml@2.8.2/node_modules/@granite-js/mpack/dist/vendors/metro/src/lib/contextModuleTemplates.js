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
var contextModuleTemplates_exports = {};
__export(contextModuleTemplates_exports, {
  getContextModuleTemplate: () => getContextModuleTemplate
});
module.exports = __toCommonJS(contextModuleTemplates_exports);
var path = __toESM(require("path"));
function createFileMap(modulePath, files, processModule) {
  let mapString = "\n";
  files.slice().sort().forEach((file) => {
    let filePath = path.relative(modulePath, file);
    if (!filePath.startsWith(".")) {
      filePath = `.${path.sep}` + filePath;
    }
    const key = JSON.stringify(filePath);
    mapString += `  ${key}: { enumerable: true, get() { return ${processModule(file)}; } },
`;
  });
  return `Object.defineProperties({}, {${mapString}})`;
}
function getEmptyContextModuleTemplate(modulePath) {
  return `
function metroEmptyContext(request) {
  let e = new Error('No modules in context');
  e.code = 'MODULE_NOT_FOUND';
  throw e;
}

// Return the keys that can be resolved.
metroEmptyContext.keys = () => ([]);

// Return the module identifier for a user request.
metroEmptyContext.resolve = function metroContextResolve(request) {
  throw new Error('Unimplemented Metro module context functionality');
}

module.exports = metroEmptyContext;`;
}
function getLoadableContextModuleTemplate(modulePath, files, importSyntax, getContextTemplate) {
  return `// All of the requested modules are loaded behind enumerable getters.
const map = ${createFileMap(modulePath, files, (moduleId) => `${importSyntax}(${JSON.stringify(moduleId)})`)};
  
function metroContext(request) {
  ${getContextTemplate}
}
  
// Return the keys that can be resolved.
metroContext.keys = function metroContextKeys() {
  return Object.keys(map);
};

// Return the module identifier for a user request.
metroContext.resolve = function metroContextResolve(request) {
  throw new Error('Unimplemented Metro module context functionality');
}

module.exports = metroContext;`;
}
function getContextModuleTemplate(mode, modulePath, files) {
  if (!files.length) {
    return getEmptyContextModuleTemplate(modulePath);
  }
  switch (mode) {
    case "eager":
      return getLoadableContextModuleTemplate(
        modulePath,
        files,
        // NOTE(EvanBacon): It's unclear if we should use `import` or `require` here so sticking
        // with the more stable option (`require`) for now.
        "require",
        [
          "  // Here Promise.resolve().then() is used instead of new Promise() to prevent",
          "  // uncaught exception popping up in devtools",
          "  return Promise.resolve().then(() => map[request]);"
        ].join("\n")
      );
    case "sync":
      return getLoadableContextModuleTemplate(modulePath, files, "require", "  return map[request];");
    case "lazy":
    case "lazy-once":
      return getLoadableContextModuleTemplate(modulePath, files, "import", "  return map[request];");
    default:
      throw new Error(`Metro context mode "${mode}" is unimplemented`);
  }
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  getContextModuleTemplate
});
