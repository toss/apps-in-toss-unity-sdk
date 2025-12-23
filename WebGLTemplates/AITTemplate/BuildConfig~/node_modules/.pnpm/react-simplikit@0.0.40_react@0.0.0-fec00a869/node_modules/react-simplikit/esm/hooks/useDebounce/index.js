// src/hooks/useDebounce/useDebounce.ts
import { useEffect as useEffect2 } from "react";
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

// src/hooks/useDebounce/useDebounce.ts
function useDebounce(callback, wait, options = {}) {
  const preservedCallback = usePreservedCallback(callback);
  const { leading = false, trailing = true } = options;
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
  const debounced = useMemo(() => {
    return debounce(preservedCallback, wait, { edges });
  }, [preservedCallback, wait, edges]);
  useEffect2(() => {
    return () => {
      debounced.cancel();
    };
  }, [debounced]);
  return debounced;
}
export {
  useDebounce
};
