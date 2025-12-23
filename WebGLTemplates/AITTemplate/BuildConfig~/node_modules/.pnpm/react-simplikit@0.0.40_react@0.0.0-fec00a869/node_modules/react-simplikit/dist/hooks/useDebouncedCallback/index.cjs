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

// src/hooks/useDebouncedCallback/index.ts
var useDebouncedCallback_exports = {};
__export(useDebouncedCallback_exports, {
  useDebouncedCallback: () => useDebouncedCallback
});
module.exports = __toCommonJS(useDebouncedCallback_exports);

// src/hooks/useDebouncedCallback/useDebouncedCallback.ts
var import_react2 = require("react");

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

// src/hooks/useDebouncedCallback/useDebouncedCallback.ts
function useDebouncedCallback({
  onChange,
  timeThreshold,
  leading = false,
  trailing = true
}) {
  const handleChange = usePreservedCallback(onChange);
  const ref = (0, import_react2.useRef)({ value: false, clearPreviousDebounce: () => {
  } });
  (0, import_react2.useEffect)(() => {
    const current = ref.current;
    return () => {
      current.clearPreviousDebounce();
    };
  }, []);
  const edges = (0, import_react2.useMemo)(() => {
    const _edges = [];
    if (leading) {
      _edges.push("leading");
    }
    if (trailing) {
      _edges.push("trailing");
    }
    return _edges;
  }, [leading, trailing]);
  return (0, import_react2.useCallback)(
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
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  useDebouncedCallback
});
