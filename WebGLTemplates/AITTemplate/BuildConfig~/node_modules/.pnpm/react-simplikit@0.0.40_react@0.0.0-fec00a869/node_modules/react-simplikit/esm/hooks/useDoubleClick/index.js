// src/hooks/useDoubleClick/useDoubleClick.ts
import { useCallback as useCallback2, useEffect as useEffect2, useRef as useRef2 } from "react";

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

// src/hooks/useDoubleClick/useDoubleClick.ts
function useDoubleClick({
  delay = 250,
  click,
  doubleClick
}) {
  const clickTimeout = useRef2(null);
  const clearClickTimeout = usePreservedCallback(() => {
    if (clickTimeout.current != null) {
      window.clearTimeout(clickTimeout.current);
      clickTimeout.current = null;
    }
  });
  useEffect2(() => () => clearClickTimeout(), [clearClickTimeout]);
  const handleEvent = useCallback2(
    (event) => {
      clearClickTimeout();
      if (click && event.detail === 1) {
        clickTimeout.current = window.setTimeout(() => {
          click(event);
        }, delay);
      }
      if (event.detail === 2) {
        doubleClick(event);
      }
    },
    [click, doubleClick, delay, clearClickTimeout]
  );
  return handleEvent;
}
export {
  useDoubleClick
};
