// src/hooks/useInterval/useInterval.ts
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

// src/hooks/useInterval/useInterval.ts
function useInterval(callback, options) {
  const delay = typeof options === "number" ? options : options.delay;
  const immediate = typeof options === "number" ? false : options.immediate;
  const enabled = typeof options === "number" ? true : options.enabled ?? true;
  const preservedCallback = usePreservedCallback(callback);
  useEffect2(() => {
    if (immediate === true && enabled) {
      preservedCallback();
    }
  }, [immediate, preservedCallback, enabled]);
  useEffect2(() => {
    if (!enabled) {
      return;
    }
    const id = window.setInterval(preservedCallback, delay);
    return () => window.clearInterval(id);
  }, [delay, preservedCallback, enabled]);
}
export {
  useInterval
};
