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
var swc_exports = {};
__export(swc_exports, {
  swcHelperOptimizationRules: () => swcHelperOptimizationRules
});
module.exports = __toCommonJS(swc_exports);
var import_path = __toESM(require("path"));
const swcHelperOptimizationRules = {
  jsc: {
    externalHelpers: true
  },
  getAliasConfig: () => {
    const swcHelperPath = import_path.default.dirname(require.resolve("@swc/helpers/package.json"));
    function getHelperName(source) {
      const tokens = source.split("/");
      return tokens[tokens.length - 1];
    }
    return {
      from: "@swc/helpers",
      /**
       * swc 구성의 externalHelpers 를 활성화 시켜 중복되는 유틸 함수를 `@swc/helpers` 패키지로 대체할 수 있으나,
       * Yarn PnP 환경에서는 실제 모듈 의존성에 포함되지 않은 `@swc/helpers` 를 resolve 할 수 없기에 preset 기준 경로로 대체하여 resolve 처리합니다.
       *
       * ```ts
       * // AS-IS
       * import { _ as _foo } from '@swc/helpers/_/_foo';
       *
       * // TO-BE
       * import { _ as _foo } from '/path/to/helpers/esm/_foo';
       * ```
       *
       * @see exports {@link https://github.com/swc-project/swc/blob/main/packages/helpers/package.json#L41}
       */
      to: function resolveSwcHelper(context) {
        return `${swcHelperPath}/esm/${getHelperName(context.args.path)}.js`;
      }
    };
  }
};
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  swcHelperOptimizationRules
});
