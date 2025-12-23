"use strict";
const { greatestLowerBound } = require("metro-source-map/src/Consumer/search");
const { SourceMetadataMapConsumer } = require("metro-symbolicate/src/Symbolication");
function createFunctionNameGetter(module2) {
  const consumer = new SourceMetadataMapConsumer(
    {
      version: 3,
      mappings: "",
      sources: ["dummy"],
      names: [],
      x_facebook_sources: [[module2.functionMap]]
    },
    (name) => name
    /* no normalization needed */
  );
  return ({ line1Based, column0Based }) => consumer.functionNameFor({
    line: line1Based,
    column: column0Based,
    source: "dummy"
  });
}
async function symbolicate(stack, maps, config) {
  const mapsByUrl = /* @__PURE__ */ new Map();
  for (const [url, map] of maps) {
    mapsByUrl.set(url, map);
  }
  const functionNameGetters = /* @__PURE__ */ new Map();
  function findModule(frame) {
    const map = mapsByUrl.get(frame.file);
    if (!map || frame.lineNumber == null) {
      return null;
    }
    const moduleIndex = greatestLowerBound(
      map,
      frame.lineNumber,
      (target, candidate) => target - candidate.firstLine1Based
    );
    if (moduleIndex == null) {
      return null;
    }
    return map[moduleIndex];
  }
  function findOriginalPos(frame, module2) {
    if (module2.map == null || frame.lineNumber == null || frame.column == null) {
      return null;
    }
    const generatedPosInModule = {
      line1Based: frame.lineNumber - module2.firstLine1Based + 1,
      column0Based: frame.column
    };
    const mappingIndex = greatestLowerBound(module2.map, generatedPosInModule, (target, candidate) => {
      if (target.line1Based === candidate[0]) {
        return target.column0Based - candidate[1];
      }
      return target.line1Based - candidate[0];
    });
    if (mappingIndex == null) {
      return null;
    }
    const mapping = module2.map[mappingIndex];
    if (mapping[0] !== generatedPosInModule.line1Based || mapping.length < 4) {
      return null;
    }
    return {
      // $FlowFixMe: Length checks do not refine tuple unions.
      line1Based: mapping[2],
      // $FlowFixMe: Length checks do not refine tuple unions.
      column0Based: mapping[3]
    };
  }
  function findFunctionName(originalPos, module2) {
    if (module2.functionMap) {
      let getFunctionName = functionNameGetters.get(module2);
      if (!getFunctionName) {
        getFunctionName = createFunctionNameGetter(module2);
        functionNameGetters.set(module2, getFunctionName);
      }
      return getFunctionName(originalPos);
    }
    return null;
  }
  function symbolicateFrame(frame) {
    const module2 = findModule(frame);
    if (!module2) {
      return frame;
    }
    if (!Array.isArray(module2.map)) {
      throw new Error(`Unexpected module with serialized source map found: ${module2.path}`);
    }
    const originalPos = findOriginalPos(frame, module2);
    if (!originalPos) {
      return frame;
    }
    const methodName = findFunctionName(originalPos, module2) ?? frame.methodName;
    return {
      ...frame,
      methodName,
      file: module2.path,
      lineNumber: originalPos.line1Based,
      column: originalPos.column0Based
    };
  }
  async function customizeFrame(frame) {
    const customizations = await config.symbolicator.customizeFrame(frame) || {};
    return { ...frame, collapse: false, ...customizations };
  }
  return Promise.all(stack.map(symbolicateFrame).map(customizeFrame));
}
module.exports = symbolicate;
