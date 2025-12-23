"use strict";
const MAGIC_NUMBER = 4293379011;
function getFileHeader(moduleCount) {
  const buffer = Buffer.alloc(8);
  buffer.writeUInt32LE(MAGIC_NUMBER, 0);
  buffer.writeUInt32LE(moduleCount, 4);
  return buffer;
}
function addModuleHeader(buffer) {
  const { getFileLength } = require("metro-hermes-compiler");
  const fileLength = getFileLength(buffer, 0);
  const header = Buffer.alloc(4);
  header.writeUInt32LE(fileLength, 0);
  return [header, buffer];
}
function bundleToBytecode(bundle) {
  const buffers = [];
  if (bundle.pre.length) {
    buffers.push(...bundle.pre);
  }
  const modules = [];
  const sortedModules = bundle.modules.slice().sort((a, b) => a[0] - b[0]);
  for (const [id, bytecodeBundle] of sortedModules) {
    buffers.push(...bytecodeBundle);
    modules.push([id, bytecodeBundle[bytecodeBundle.length - 1].length]);
  }
  if (bundle.post.length) {
    buffers.push(...bundle.post);
  }
  return {
    bytecode: Buffer.concat([getFileHeader(buffers.length), ...buffers.flatMap(addModuleHeader)]),
    metadata: {
      pre: bundle.pre ? bundle.pre.length : 0,
      post: bundle.post.length,
      modules
    }
  };
}
module.exports = bundleToBytecode;
module.exports.MAGIC_NUMBER = MAGIC_NUMBER;
