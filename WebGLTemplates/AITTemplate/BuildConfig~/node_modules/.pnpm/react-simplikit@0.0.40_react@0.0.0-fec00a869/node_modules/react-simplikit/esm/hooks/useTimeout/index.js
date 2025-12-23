// src/hooks/useTimeout/useTimeout.ts
import { useEffect as useEffect2 } from "react";

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

// src/hooks/useTimeout/useTimeout.ts
function useTimeout(callback, delay = 0) {
  const preservedCallback = usePreservedCallback(callback);
  useEffect2(() => {
    const timeoutId = window.setTimeout(preservedCallback, delay);
    return () => window.clearTimeout(timeoutId);
  }, [delay, preservedCallback]);
}
export {
  useTimeout
};
