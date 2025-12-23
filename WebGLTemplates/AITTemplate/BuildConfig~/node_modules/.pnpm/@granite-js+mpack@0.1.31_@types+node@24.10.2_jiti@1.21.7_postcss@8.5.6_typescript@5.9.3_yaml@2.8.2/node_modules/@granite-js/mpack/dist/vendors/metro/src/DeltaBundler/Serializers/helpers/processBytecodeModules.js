"use strict";
const { isBytecodeModule, wrapModule } = require("./bytecode");
function processBytecodeModules(modules, { filter = () => true, createModuleId, dev, projectRoot }) {
  return [...modules].filter(isBytecodeModule).filter(filter).map((module2) => [
    module2,
    wrapModule(module2, {
      createModuleId,
      dev,
      projectRoot
    })
  ]);
}
module.exports = processBytecodeModules;
