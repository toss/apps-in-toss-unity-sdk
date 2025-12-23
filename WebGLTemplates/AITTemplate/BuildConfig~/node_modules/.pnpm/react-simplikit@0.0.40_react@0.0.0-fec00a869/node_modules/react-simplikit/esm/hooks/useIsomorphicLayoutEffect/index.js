// src/hooks/useIsomorphicLayoutEffect/useIsomorphicLayoutEffect.ts
import { useEffect, useLayoutEffect } from "react";
var isServer = typeof window === "undefined";
var useIsomorphicLayoutEffect = isServer ? useEffect : useLayoutEffect;
export {
  useIsomorphicLayoutEffect
};
