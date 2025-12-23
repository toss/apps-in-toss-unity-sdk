// src/hooks/useThrottle/useThrottle.ts
import { useEffect as useEffect2, useMemo as useMemo2 } from "react";

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

// src/hooks/usePreservedReference/usePreservedReference.ts
import { useMemo, useRef as useRef2 } from "react";
function usePreservedReference(value, areValuesEqual = areDeeplyEqual) {
  const ref = useRef2(value);
  return useMemo(() => {
    if (!areValuesEqual(ref.current, value)) {
      ref.current = value;
    }
    return ref.current;
  }, [areValuesEqual, value]);
}
function areDeeplyEqual(x, y) {
  return JSON.stringify(x) === JSON.stringify(y);
}

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

// src/hooks/useThrottle/throttle.ts
function throttle(func, throttleMs, { edges = ["leading", "trailing"] } = {}) {
  let pendingAt = null;
  const debounced = debounce(func, throttleMs, { edges });
  const throttled = function(...args) {
    if (pendingAt == null) {
      pendingAt = Date.now();
    } else {
      if (Date.now() - pendingAt >= throttleMs) {
        pendingAt = Date.now();
        debounced.cancel();
      }
    }
    debounced(...args);
  };
  throttled.cancel = debounced.cancel;
  return throttled;
}

// src/hooks/useThrottle/useThrottle.ts
function useThrottle(callback, wait, options) {
  const preservedCallback = usePreservedCallback(callback);
  const preservedOptions = usePreservedReference(options ?? {});
  const throttledCallback = useMemo2(
    () => throttle(preservedCallback, wait, preservedOptions),
    [preservedOptions, preservedCallback, wait]
  );
  useEffect2(() => {
    return () => {
      throttledCallback.cancel();
    };
  }, [throttledCallback]);
  return throttledCallback;
}
export {
  useThrottle
};
