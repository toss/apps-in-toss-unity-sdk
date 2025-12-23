"use strict";
const sourceMap = require("source-map");
function mergeSourceMaps(file, originalMap, secondMap) {
  const merged = new sourceMap.SourceMapGenerator();
  const inputMap = new sourceMap.SourceMapConsumer(originalMap);
  new sourceMap.SourceMapConsumer(secondMap).eachMapping((mapping) => {
    if (mapping.originalLine == null) {
      return;
    }
    const original = inputMap.originalPositionFor({
      line: mapping.originalLine,
      column: mapping.originalColumn
    });
    if (original.line == null) {
      return;
    }
    merged.addMapping({
      generated: { line: mapping.generatedLine, column: mapping.generatedColumn },
      original: { line: original.line, column: original.column || 0 },
      source: file,
      name: original.name || mapping.name
    });
  });
  return {
    ...merged.toJSON(),
    sources: inputMap.sources
  };
}
module.exports = mergeSourceMaps;
