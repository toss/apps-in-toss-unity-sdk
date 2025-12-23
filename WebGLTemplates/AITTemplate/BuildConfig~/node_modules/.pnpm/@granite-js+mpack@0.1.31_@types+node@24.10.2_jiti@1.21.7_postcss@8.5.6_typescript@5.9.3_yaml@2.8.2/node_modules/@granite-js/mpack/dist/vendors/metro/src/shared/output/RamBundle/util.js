"use strict";
var __create = Object.create;
var __defProp = Object.defineProperty;
var __getOwnPropDesc = Object.getOwnPropertyDescriptor;
var __getOwnPropNames = Object.getOwnPropertyNames;
var __getProtoOf = Object.getPrototypeOf;
var __hasOwnProp = Object.prototype.hasOwnProperty;
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
var import_countLines = __toESM(require("../../../lib/countLines"));
const invariant = require("invariant");
function lineToLineSourceMap(source, filename = "") {
  const firstLine = "AAAA;";
  const line = "AACA;";
  return {
    file: filename,
    mappings: firstLine + Array((0, import_countLines.default)(source)).join(line),
    sources: [filename],
    names: [],
    version: 3
  };
}
const wrapperEnd = (wrappedCode) => wrappedCode.indexOf("{") + 1;
const Section = (line, column, map) => ({
  map,
  offset: { line, column }
});
function combineSourceMaps(modules, moduleGroups, options) {
  const sections = combineMaps(modules, null, moduleGroups, options);
  return { sections, version: 3 };
}
function combineSourceMapsAddingOffsets(modules, x_metro_module_paths, moduleGroups, options) {
  const x_facebook_offsets = [];
  const sections = combineMaps(modules, x_facebook_offsets, moduleGroups, options);
  return { sections, version: 3, x_facebook_offsets, x_metro_module_paths };
}
function combineMaps(modules, offsets, moduleGroups, options) {
  const sections = [];
  let line = 0;
  modules.forEach((moduleTransport) => {
    const { code, id, name } = moduleTransport;
    let column = 0;
    let group;
    let groupLines = 0;
    let { map } = moduleTransport;
    if (moduleGroups && moduleGroups.modulesInGroups.has(id)) {
      return;
    }
    if (offsets != null) {
      group = moduleGroups && moduleGroups.groups.get(id);
      if (group && moduleGroups) {
        const { modulesById } = moduleGroups;
        const otherModules = Array.from(group || []).map((moduleId) => modulesById.get(moduleId)).filter(Boolean);
        otherModules.forEach((m) => {
          groupLines += (0, import_countLines.default)(m.code);
        });
        map = combineSourceMaps([moduleTransport].concat(otherModules));
      }
      column = options && options.fixWrapperOffset ? wrapperEnd(code) : 0;
    }
    invariant(!Array.isArray(map), "Random Access Bundle source maps cannot be built from raw mappings");
    sections.push(Section(line, column, map || lineToLineSourceMap(code, name)));
    if (offsets != null && id != null) {
      offsets[id] = line;
      for (const moduleId of group || []) {
        offsets[moduleId] = line;
      }
    }
    line += (0, import_countLines.default)(code) + groupLines;
  });
  return sections;
}
const joinModules = (modules) => modules.map((m) => m.code).join("\n");
module.exports = {
  combineSourceMaps,
  combineSourceMapsAddingOffsets,
  countLines: import_countLines.default,
  joinModules,
  lineToLineSourceMap
};
