"use strict";
const { sourceMapGenerator } = require("./sourceMapGenerator");
function sourceMapString(modules, options) {
  return sourceMapGenerator(modules, options).toString(void 0, {
    excludeSource: options.excludeSource
  });
}
module.exports = sourceMapString;
