"use strict";
exports.empty = () => virtual("", "/<generated>/empty.js");
const virtual = (code, filePath) => ({
  dependencies: [],
  file: {
    code,
    map: null,
    functionMap: null,
    path: filePath,
    type: "script",
    libraryIdx: null
  }
});
exports.virtual = virtual;
