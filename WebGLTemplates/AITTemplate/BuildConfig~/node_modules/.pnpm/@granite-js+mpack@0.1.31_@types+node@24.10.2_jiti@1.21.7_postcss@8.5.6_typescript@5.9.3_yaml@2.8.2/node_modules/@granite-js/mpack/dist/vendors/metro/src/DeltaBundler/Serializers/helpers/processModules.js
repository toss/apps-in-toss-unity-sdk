"use strict";
const { isJsModule, wrapModule } = require("./js");
function processModules(modules, { filter = () => true, createModuleId, dev, projectRoot }) {
  return [...modules].filter(isJsModule).filter(filter).map((module2) => [
    module2,
    wrapModule(module2, {
      createModuleId,
      dev,
      projectRoot
    })
  ]);
}
module.exports = processModules;
