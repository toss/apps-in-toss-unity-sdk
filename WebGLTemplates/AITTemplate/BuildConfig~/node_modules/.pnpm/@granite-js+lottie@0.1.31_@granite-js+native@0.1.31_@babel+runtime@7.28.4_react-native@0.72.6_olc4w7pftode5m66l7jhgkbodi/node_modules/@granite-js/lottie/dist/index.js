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

// src/index.ts
var index_exports = {};
__export(index_exports, {
  Lottie: () => Lottie
});
module.exports = __toCommonJS(index_exports);

// src/Lottie.tsx
var import_lottie_react_native = __toESM(require("@granite-js/native/lottie-react-native"));
var import_react_native2 = require("react-native");

// src/ensureSafeLottie.ts
var import_react_native = require("react-native");
function ensureSafeLottie(jsonData) {
  if (import_react_native.Platform.OS === "android") {
    return {
      ...jsonData,
      fonts: {
        list: []
      }
    };
  } else {
    return jsonData;
  }
}
function hasFonts(jsonData) {
  if (jsonData && "fonts" in jsonData) {
    if ("list" in jsonData.fonts) {
      return jsonData.fonts.list.length > 0;
    }
  }
  return false;
}

// src/useFetchResource.tsx
var import_react = require("react");
function useFetchResource(src, onError) {
  const [data, setData] = (0, import_react.useState)(null);
  (0, import_react.useEffect)(() => {
    async function run() {
      const response = await fetch(src);
      setData(await response.json());
    }
    run().catch(
      onError ?? ((e) => {
        throw e;
      })
    );
  }, [src, onError]);
  return data;
}

// src/Lottie.tsx
var import_jsx_runtime = require("react/jsx-runtime");
function Lottie({
  width,
  maxWidth,
  height,
  src,
  autoPlay = true,
  speed = 1,
  style,
  onAnimationFailure,
  ...props
}) {
  const jsonData = useFetchResource(src, onAnimationFailure);
  if (jsonData == null) {
    return /* @__PURE__ */ (0, import_jsx_runtime.jsx)(import_react_native2.View, { testID: "lottie-placeholder", style: [{ opacity: 1, width, height }, style] });
  }
  if (hasFonts(jsonData) && __DEV__) {
    throw new Error(
      `The Lottie resource contains custom fonts which is unsafe. Please remove the custom fonts. source: ${src}`
    );
  }
  return /* @__PURE__ */ (0, import_jsx_runtime.jsx)(
    import_lottie_react_native.default,
    {
      source: ensureSafeLottie(jsonData),
      autoPlay,
      speed,
      style: [{ width, height, maxWidth }, style],
      onAnimationFailure,
      ...props
    }
  );
}
Lottie.AnimationObject = function LottieWithAnimationObject({
  width,
  maxWidth,
  height,
  animationObject,
  autoPlay = true,
  speed = 1,
  style,
  onAnimationFailure,
  ...props
}) {
  return /* @__PURE__ */ (0, import_jsx_runtime.jsx)(
    import_lottie_react_native.default,
    {
      source: animationObject,
      autoPlay,
      speed,
      style: [{ width, height, maxWidth }, style],
      onAnimationFailure,
      ...props
    }
  );
};
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  Lottie
});
