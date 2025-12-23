// src/hooks/useDebouncedCallback/useDebouncedCallback.ts
import { useCallback as useCallback2, useEffect as useEffect2, useMemo, useRef as useRef2 } from "react";

// src/hooks/useDebounce/debounce.ts
function debounce(func, debounceMs, { edges = ["leading", "trailing"] } = {}) {
  let pendingThis = void 0;
  let pendingArgs = null;
  const leading = edges != null && edges.includes("leading");
  const trailing = edges == null || edges.includes("trailing");
  const invoke = () => {
    if (pendingArgs !== null) {
      func.apply(pendingThis, pendingArgs);
      pendingThis = void 0;
      pendingArgs = null;
    }
  };
  const onTimerEnd = () => {
    if (trailing) {
      invoke();
    }
    cancel();
  };
  let timeoutId = null;
  const schedule = () => {
    if (timeoutId != null) {
      clearTimeout(timeoutId);
    }
    timeoutId = setTimeout(() => {
      timeoutId = null;
      onTimerEnd();
    }, debounceMs);
  };
  const cancelTimer = () => {
    if (timeoutId !== null) {
      clearTimeout(timeoutId);
      timeoutId = null;
    }
  };
  const cancel = () => {
    cancelTimer();
    pendingThis = void 0;
    pendingArgs = null;
  };
  const debounced = function(...args) {
    pendingThis = this;
    pendingArgs = args;
    const isFirstCall = timeoutId == null;
    schedule();
    if (leading && isFirstCall) {
      invoke();
    }
  };
  debounced.cancel = cancel;
  return debounced;
}

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

// src/hooks/useDebouncedCallback/useDebouncedCallback.ts
function useDebouncedCallback({
  onChange,
  timeThreshold,
  leading = false,
  trailing = true
}) {
  const handleChange = usePreservedCallback(onChange);
  const ref = useRef2({ value: false, clearPreviousDebounce: () => {
  } });
  useEffect2(() => {
    const current = ref.current;
    return () => {
      current.clearPreviousDebounce();
    };
  }, []);
  const edges = useMemo(() => {
    const _edges = [];
    if (leading) {
      _edges.push("leading");
    }
    if (trailing) {
      _edges.push("trailing");
    }
    return _edges;
  }, [leading, trailing]);
  return useCallback2(
    (nextValue) => {
      if (nextValue === ref.current.value) {
        return;
      }
      const debounced = debounce(
        () => {
          handleChange(nextValue);
          ref.current.value = nextValue;
        },
        timeThreshold,
        { edges }
      );
      ref.current.clearPreviousDebounce();
      debounced();
      ref.current.clearPreviousDebounce = debounced.cancel;
    },
    [handleChange, timeThreshold, edges]
  );
}
export {
  useDebouncedCallback
};
