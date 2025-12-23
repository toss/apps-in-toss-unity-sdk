"use strict";
const { getJsOutput } = require("./js");
function getSourceMapInfo(module2, options) {
  return {
    ...getJsOutput(module2).data,
    path: module2.path,
    source: options.excludeSource ? "" : getModuleSource(module2)
  };
}
function getModuleSource(module2) {
  if (getJsOutput(module2).type === "js/module/asset") {
    return "";
  }
  return module2.getSource().toString();
}
module.exports = getSourceMapInfo;
