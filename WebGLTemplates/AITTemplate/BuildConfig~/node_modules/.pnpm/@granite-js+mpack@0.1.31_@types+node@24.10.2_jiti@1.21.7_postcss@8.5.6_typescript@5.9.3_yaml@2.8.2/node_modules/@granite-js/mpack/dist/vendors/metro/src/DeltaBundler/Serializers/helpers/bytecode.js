"use strict";
const invariant = require("invariant");
const path = require("path");
function wrapModule(module2, options) {
  const output = getBytecodeOutput(module2);
  if (output.type.startsWith("bytecode/script")) {
    return [output.data.bytecode];
  }
  const params = [
    options.createModuleId(module2.path),
    "[" + Array.from(module2.dependencies.values()).map((dependency) => options.createModuleId(dependency.absolutePath)).join(",") + "]"
  ];
  if (options.dev) {
    params.push(JSON.stringify(path.relative(options.projectRoot, module2.path)));
  }
  const { compile } = require("metro-hermes-compiler");
  const headerCode = `globalThis.$$METRO_D=[${params.join(",")}];`;
  return [
    compile(headerCode, {
      sourceURL: module2.path + "-virtual.js"
    }).bytecode,
    output.data.bytecode
  ];
}
function getBytecodeOutput(module2) {
  const output = module2.output.filter(({ type }) => type.startsWith("bytecode/")).map(
    (output2) => output2.data.bytecode instanceof Buffer ? output2 : (
      // Re-create buffers after losing the Buffer instance when sending data over workers.
      {
        ...output2,
        data: {
          ...output2.data,
          bytecode: Buffer.from(output2.data.bytecode.data)
        }
      }
    )
  );
  invariant(
    output.length === 1,
    `Modules must have exactly one bytecode output, but ${module2.path} has ${output.length} bytecode outputs.`
  );
  return output[0];
}
function isBytecodeModule(module2) {
  return module2.output.filter(({ type }) => type.startsWith("bytecode/")).length > 0;
}
module.exports = {
  getBytecodeOutput,
  isBytecodeModule,
  wrapModule
};
