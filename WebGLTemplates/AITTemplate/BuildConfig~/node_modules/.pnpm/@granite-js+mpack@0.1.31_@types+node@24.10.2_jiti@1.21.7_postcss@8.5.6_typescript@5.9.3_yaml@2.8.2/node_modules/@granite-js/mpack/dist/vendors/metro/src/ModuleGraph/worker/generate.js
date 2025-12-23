"use strict";
const babelGenerate = require("@babel/generator").default;
function generate(ast, filename, sourceCode) {
  const generated = babelGenerate(
    ast,
    {
      comments: false,
      compact: true,
      filename,
      sourceFileName: filename,
      sourceMaps: true,
      sourceMapTarget: filename
    },
    sourceCode
  );
  if (generated.map) {
    delete generated.map.sourcesContent;
  }
  return generated;
}
module.exports = generate;
