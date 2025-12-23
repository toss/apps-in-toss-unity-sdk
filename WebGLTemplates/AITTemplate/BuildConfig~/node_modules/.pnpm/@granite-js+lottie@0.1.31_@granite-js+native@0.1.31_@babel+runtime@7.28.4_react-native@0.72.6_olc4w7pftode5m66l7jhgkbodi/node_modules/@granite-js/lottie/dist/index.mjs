// src/Lottie.tsx
import LottieView from "@granite-js/native/lottie-react-native";
import { View } from "react-native";

// src/ensureSafeLottie.ts
import { Platform } from "react-native";
function ensureSafeLottie(jsonData) {
  if (Platform.OS === "android") {
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
import { useEffect, useState } from "react";
function useFetchResource(src, onError) {
  const [data, setData] = useState(null);
  useEffect(() => {
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
import { jsx } from "react/jsx-runtime";
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
    return /* @__PURE__ */ jsx(View, { testID: "lottie-placeholder", style: [{ opacity: 1, width, height }, style] });
  }
  if (hasFonts(jsonData) && __DEV__) {
    throw new Error(
      `The Lottie resource contains custom fonts which is unsafe. Please remove the custom fonts. source: ${src}`
    );
  }
  return /* @__PURE__ */ jsx(
    LottieView,
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
  return /* @__PURE__ */ jsx(
    LottieView,
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
export {
  Lottie
};
