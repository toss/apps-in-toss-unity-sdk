// src/hooks/useCallbackOncePerRender/useCallbackOncePerRender.ts
import { useEffect as useEffect2, useRef as useRef2 } from "react";

// src/hooks/usePreservedCallback/usePreservedCallback.ts
import { useCallback, useEffect, useRef } from "react";
function usePreservedCallback(callback) {
  const callbackRef = useRef(callback);
  useEffect(() => {
    callbackRef.current = callback;
  }, [callback]);
  return useCallback((...args) => {
    return callbackRef.current(...args);
  }, []);
}

// src/hooks/useCallbackOncePerRender/useCallbackOncePerRender.ts
function useCallbackOncePerRender(callback, deps) {
  const hasFired = useRef2(false);
  useEffect2(() => {
    hasFired.current = false;
  }, deps);
  return usePreservedCallback((...args) => {
    if (hasFired.current) {
      return;
    }
    callback(...args);
    hasFired.current = true;
  });
}
export {
  useCallbackOncePerRender
};
