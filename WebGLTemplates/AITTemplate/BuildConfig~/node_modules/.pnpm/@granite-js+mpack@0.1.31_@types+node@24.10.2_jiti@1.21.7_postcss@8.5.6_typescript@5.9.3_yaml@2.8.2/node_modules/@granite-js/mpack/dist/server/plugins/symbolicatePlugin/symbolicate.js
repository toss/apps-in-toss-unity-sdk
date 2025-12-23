"use strict";
var __create = Object.create;
var __defProp = Object.defineProperty;
var __getOwnPropDesc = Object.getOwnPropertyDescriptor;
var __getOwnPropNames = Object.getOwnPropertyNames;
var __getProtoOf = Object.getPrototypeOf;
var __hasOwnProp = Object.prototype.hasOwnProperty;
var __export = (target, all) => {
  for (var name in all)
    __defProp(target, name, { get: all[name], enumerable: true });
};
var __copyProps = (to, from, except, desc) => {
  if (from && typeof from === "object" || typeof from === "function") {
    for (let key of __getOwnPropNames(from))
      if (!__hasOwnProp.call(to, key) && key !== except)
        __defProp(to, key, { get: () => from[key], enumerable: !(desc = __getOwnPropDesc(from, key)) || desc.enumerable });
  }
  return to;
};
var __toESM = (mod, isNodeMode, target) => (target = mod != null ? __create(__getProtoOf(mod)) : {}, __copyProps(
  // If the importer is in node compatibility mode or this is not an ESM
  // file that has been converted to a CommonJS file using a Babel-
  // compatible transform (i.e. "__esModule" has not been set), then set
  // "default" to the CommonJS "module.exports" for node compatibility.
  isNodeMode || !mod || !mod.__esModule ? __defProp(target, "default", { value: mod, enumerable: true }) : target,
  mod
));
var __toCommonJS = (mod) => __copyProps(__defProp({}, "__esModule", { value: true }), mod);
var symbolicate_exports = {};
__export(symbolicate_exports, {
  symbolicate: () => symbolicate
});
module.exports = __toCommonJS(symbolicate_exports);
var import_path = __toESM(require("path"));
var import_code_frame = require("@babel/code-frame");
var import_source_map = __toESM(require("source-map"));
async function symbolicate(rawSourcemap, stackFrame) {
  const { stack, codeFrame } = await import_source_map.default.SourceMapConsumer.with(
    Buffer.from(rawSourcemap).toString("utf-8"),
    null,
    (consumer) => {
      const originalStackFrame = stackFrame.map((frame) => {
        const originalPosition = consumer.originalPositionFor({
          column: frame.column,
          line: frame.lineNumber
        });
        return originalPosition.line == null ? {
          collapse: true,
          column: originalPosition.column ?? frame.column,
          line: originalPosition.line ?? frame.lineNumber,
          name: originalPosition.name ?? frame.methodName,
          source: originalPosition.source ?? frame.file
        } : originalPosition;
      });
      const targetFrame = originalStackFrame.find((frame) => !("collapse" in frame));
      const content = targetFrame?.source && consumer.sourceContentFor(targetFrame?.source);
      let codeFrame2 = null;
      if (targetFrame != null && content != null) {
        codeFrame2 = {
          content: (0, import_code_frame.codeFrameColumns)(content, { start: targetFrame }, { highlightCode: true }),
          location: { column: targetFrame.column, row: targetFrame.line },
          fileName: import_path.default.basename(targetFrame.source)
        };
      }
      return {
        /**
         * React Native 와 source-map 라이브러리가 서로 호환되지 않아, React Native 에 맞게 데이터 매핑
         */
        stack: originalStackFrame.map(({ column, line, source, name }) => ({
          column,
          lineNumber: line,
          file: source,
          methodName: name
        })),
        codeFrame: codeFrame2
      };
    }
  );
  return { stack, codeFrame };
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  symbolicate
});
