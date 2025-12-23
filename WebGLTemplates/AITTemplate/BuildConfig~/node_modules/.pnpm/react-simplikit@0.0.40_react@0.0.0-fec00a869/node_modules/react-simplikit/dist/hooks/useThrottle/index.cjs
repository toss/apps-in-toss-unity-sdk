"use strict";
var __defProp = Object.defineProperty;
var __getOwnPropDesc = Object.getOwnPropertyDescriptor;
var __getOwnPropNames = Object.getOwnPropertyNames;
var __hasOwnProp = Object.prototype.hasOwnProperty;
var __export = (target, all) => {
  for (var name in all)
    __defProp(target, name, { get: all[name], enumerable: true });
};
var __copyProps = (to, from, except, desc) => {
  if (from && typeof from === "object" || typeof from === "function") {
    for (let key of __getOwnPropNames(from))
      if (!__hasOwnProp.call(to, key) && key !== except)
        __defProp(to, key, { get: () => from[key], enumerable: !(desc = __getOwnPropDesc(from, key)) || desc.enumerable });
  }
  return to;
};
var __toCommonJS = (mod) => __copyProps(__defProp({}, "__esModule", { value: true }), mod);

// src/hooks/useThrottle/index.ts
var useThrottle_exports = {};
__export(useThrottle_exports, {
  useThrottle: () => useThrottle
});
module.exports = __toCommonJS(useThrottle_exports);

// src/hooks/useThrottle/useThrottle.ts
var import_react3 = require("react");

// src/hooks/usePreservedCallback/usePreservedCallback.ts
var import_react = require("react");
function usePreservedCallback(callback) {
  const callbackRef = (0, import_react.useRef)(callback);
  (0, import_react.useEffect)(() => {
    callbackRef.current = callback;
  }, [callback]);
  return (0, import_react.useCallback)((...args) => {
    return callbackRef.current(...args);
  }, []);
}

// src/hooks/usePreservedReference/usePreservedReference.ts
var import_react2 = require("react");
function usePreservedReference(value, areValuesEqual = areDeeplyEqual) {
  const ref = (0, import_react2.useRef)(value);
  return (0, import_react2.useMemo)(() => {
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
  const throttledCallback = (0, import_react3.useMemo)(
    () => throttle(preservedCallback, wait, preservedOptions),
    [preservedOptions, preservedCallback, wait]
  );
  (0, import_react3.useEffect)(() => {
    return () => {
      throttledCallback.cancel();
    };
  }, [throttledCallback]);
  return throttledCallback;
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  useThrottle
});
