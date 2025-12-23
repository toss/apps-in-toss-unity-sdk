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
var createFullyTransformStep_exports = {};
__export(createFullyTransformStep_exports, {
  createFullyTransformStep: () => createFullyTransformStep
});
module.exports = __toCommonJS(createFullyTransformStep_exports);
var path = __toESM(require("path"));
var babel = __toESM(require("@babel/core"));
var import_defineStepName = require("../../../../utils/defineStepName");
function createFullyTransformStep({
  dev,
  additionalBabelOptions
}) {
  const baseOptions = {
    configFile: additionalBabelOptions?.configFile || false,
    presets: [
      [
        /**
         * React Native Hermes 대응을 위해
         * 최대한 낮은 버전의 JS 엔진을 지원하도록 IE 11로 설정
         * 추후 정확한 target으로 설정 필요
         */
        require.resolve("@babel/preset-env"),
        {
          targets: {
            ie: 11
          },
          /**
           * supportsStaticESM 이 true 이면 modules 가 false 로 처리되어야 하는데,
           * 기본값('auto')이 적용되어 안내 로그가 찍히고 있어 직접 값 지정
           *
           * @see source {@link https://github.com/babel/babel/blob/v7.23.10/packages/babel-preset-env/src/index.ts#L398-L403}
           */
          modules: false
        }
      ],
      /**
       * react-native-reanimated 등 TypeScript를 직접 export하는 라이브러리를 다루기 위해
       * @babel/preset-typescript 포함 필요
       */
      require.resolve("@babel/preset-typescript"),
      /**
       * react-native-reanimated 등 .tsx 직접 export하는 라이브러리를 다루기 위해
       * @babel/preset-react 포함 필요
       */
      [require.resolve("@babel/preset-react"), { runtime: "automatic" }],
      ...additionalBabelOptions?.presets ?? []
    ],
    plugins: [
      /**
       * react-native에서 직접 export 하는 flow 파일 대응을 위해 strip types 추가 필요
       */
      require.resolve("@babel/plugin-transform-flow-strip-types"),
      [require.resolve("@babel/plugin-proposal-class-properties"), { loose: true }],
      [require.resolve("@babel/plugin-proposal-private-property-in-object"), { loose: true }],
      [require.resolve("@babel/plugin-proposal-private-methods"), { loose: true }],
      ...additionalBabelOptions?.plugins ?? []
    ]
  };
  const fullyTransformStep = async function fullyTransform(code, args) {
    const babelOptions = babel.loadOptions({
      minified: false,
      compact: false,
      babelrc: false,
      configFile: false,
      envName: dev ? "development" : "production",
      ...baseOptions,
      sourceMaps: "inline",
      filename: path.basename(args.path),
      caller: {
        name: "mpack-fully-transform-plugin",
        supportsStaticESM: true
      }
    });
    if (!babelOptions) {
      return { code };
    }
    if (babelOptions.sourceMaps) {
      babelOptions.sourceFileName = path.basename(args.path);
    }
    const result = await babel.transformAsync(code, babelOptions);
    if (result?.code != null) {
      return { code: result.code };
    }
    throw new Error("babel transform result is null");
  };
  (0, import_defineStepName.defineStepName)(fullyTransformStep, "fully-transform");
  return fullyTransformStep;
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  createFullyTransformStep
});
