// src/hooks/useIntersectionObserver/useIntersectionObserver.ts
import { useMemo } from "react";

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

// src/hooks/useRefEffect/useRefEffect.ts
import { useCallback as useCallback2, useRef as useRef2 } from "react";
function useRefEffect(callback, deps) {
  const preservedCallback = usePreservedCallback(callback);
  const cleanupCallbackRef = useRef2(() => {
  });
  const effect = useCallback2(
    (element) => {
      cleanupCallbackRef.current();
      cleanupCallbackRef.current = () => {
      };
      if (element == null) {
        return;
      }
      const cleanup = preservedCallback(element);
      if (typeof cleanup === "function") {
        cleanupCallbackRef.current = cleanup;
      }
    },
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [preservedCallback, ...deps]
  );
  return effect;
}

// src/hooks/useIntersectionObserver/useIntersectionObserver.ts
function useIntersectionObserver(callback, options) {
  const preservedCallback = usePreservedCallback(callback);
  const observer = useMemo(() => {
    if (typeof IntersectionObserver === "undefined") {
      return;
    }
    return new IntersectionObserver(([entry]) => {
      preservedCallback(entry);
    }, options);
  }, [preservedCallback, options]);
  return useRefEffect(
    (element) => {
      observer?.observe(element);
      return () => {
        observer?.unobserve(element);
      };
    },
    [preservedCallback, options]
  );
}
export {
  useIntersectionObserver
};
