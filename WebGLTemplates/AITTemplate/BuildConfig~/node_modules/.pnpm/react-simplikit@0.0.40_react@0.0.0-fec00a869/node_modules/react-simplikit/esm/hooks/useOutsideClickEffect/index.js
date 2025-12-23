// src/hooks/useOutsideClickEffect/useOutsideClickEffect.ts
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

// src/hooks/useOutsideClickEffect/useOutsideClickEffect.ts
function useOutsideClickEffect(container, callback) {
  const containers = useRef2([]);
  const handleDocumentClick = usePreservedCallback(({ target }) => {
    if (target === null) {
      return;
    }
    if (containers.current.length === 0) {
      return;
    }
    if (containers.current.some((x) => x.contains(target))) {
      return;
    }
    callback();
  });
  useEffect2(() => {
    containers.current = [container].flat(1).filter((item) => item != null);
  }, [container]);
  useEffect2(() => {
    document.addEventListener("click", handleDocumentClick);
    return () => {
      document.removeEventListener("click", handleDocumentClick);
    };
  }, [handleDocumentClick]);
}
export {
  useOutsideClickEffect
};
