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
var scripts_exports = {};
__export(scripts_exports, {
  getRequireContextScript: () => getRequireContextScript,
  toRequireContextExportScript: () => toRequireContextExportScript
});
module.exports = __toCommonJS(scripts_exports);
var import_constants = require("../../../constants");
function toRequireContextExportScript(content) {
  const [_, source] = content.match(/require\.context\((.*?)\)/) ?? [];
  const parsedSource = source?.replace(/['"]/g, "");
  if (!parsedSource) {
    throw new Error("\uC720\uD6A8\uD558\uC9C0 \uC54A\uC740 require context \uAD6C\uBB38\uC785\uB2C8\uB2E4");
  }
  const IMPORT_STATEMENT = `import __context__ from '${import_constants.REQUIRE_CONTEXT_PROTOCOL}${parsedSource}';`;
  const MODULE_BODY = content.replace(/require\.context\('(.*?)'\)/, "__context__").replace("const", "var");
  return `
${IMPORT_STATEMENT}

${MODULE_BODY}
`.trim();
}
function getRequireContextScript(modules) {
  const IMPORT_STATEMENTS = modules.map((module2) => `import * as module${module2.moduleIndex} from ${JSON.stringify(module2.absolutePath)};`).join("\n");
  const ASSIGN_STATEMENTS = modules.map((module2) => `_modules[${JSON.stringify(module2.relativePath)}] = module${module2.moduleIndex};`).join("\n");
  return `
${IMPORT_STATEMENTS}

var requireContext = function(key) {
  var _modules = {};
  
  ${ASSIGN_STATEMENTS}

  return _modules[key];
};

requireContext.keys = function() {
  return [${modules.map((module2) => JSON.stringify(module2.relativePath)).join(",")}];
};

export default requireContext;
`.trim();
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  getRequireContextScript,
  toRequireContextExportScript
});
