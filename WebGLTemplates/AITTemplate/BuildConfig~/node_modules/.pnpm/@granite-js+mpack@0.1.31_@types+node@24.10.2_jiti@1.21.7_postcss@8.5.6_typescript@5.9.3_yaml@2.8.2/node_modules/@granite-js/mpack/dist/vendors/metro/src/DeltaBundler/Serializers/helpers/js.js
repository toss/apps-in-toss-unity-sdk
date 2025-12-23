"use strict";
const invariant = require("invariant");
const { addParamsToDefineCall } = require("metro-transform-plugins");
const path = require("path");
function wrapModule(module2, options) {
  const output = getJsOutput(module2);
  if (output.type.startsWith("js/script")) {
    return output.data.code;
  }
  const moduleId = options.createModuleId(module2.path);
  const params = [
    moduleId,
    Array.from(module2.dependencies.values()).map((dependency) => options.createModuleId(dependency.absolutePath))
  ];
  if (options.dev) {
    params.push(path.relative(options.projectRoot, module2.path));
  }
  return addParamsToDefineCall(output.data.code, ...params);
}
function getJsOutput(module2) {
  const jsModules = module2.output.filter(({ type }) => type.startsWith("js/"));
  invariant(
    jsModules.length === 1,
    `Modules must have exactly one JS output, but ${module2.path ?? "unknown module"} has ${jsModules.length} JS outputs.`
  );
  const jsOutput = jsModules[0];
  invariant(
    Number.isFinite(jsOutput.data.lineCount),
    `JS output must populate lineCount, but ${module2.path ?? "unknown module"} has ${jsOutput.type} output with lineCount '${jsOutput.data.lineCount}'`
  );
  return jsOutput;
}
function isJsModule(module2) {
  return module2.output.filter(isJsOutput).length > 0;
}
function isJsOutput(output) {
  return output.type.startsWith("js/");
}
module.exports = {
  getJsOutput,
  isJsModule,
  wrapModule
};
