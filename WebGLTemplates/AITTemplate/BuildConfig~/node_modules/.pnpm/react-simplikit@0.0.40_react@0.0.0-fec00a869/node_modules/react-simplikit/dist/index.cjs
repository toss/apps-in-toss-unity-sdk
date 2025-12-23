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

// src/index.ts
var index_exports = {};
__export(index_exports, {
  ImpressionArea: () => ImpressionArea,
  Separated: () => Separated,
  SwitchCase: () => SwitchCase,
  buildContext: () => buildContext,
  mergeProps: () => mergeProps,
  mergeRefs: () => mergeRefs,
  useAsyncEffect: () => useAsyncEffect,
  useBooleanState: () => useBooleanState,
  useCallbackOncePerRender: () => useCallbackOncePerRender,
  useConditionalEffect: () => useConditionalEffect,
  useControlledState: () => useControlledState,
  useCounter: () => useCounter,
  useDebounce: () => useDebounce,
  useDebouncedCallback: () => useDebouncedCallback,
  useDoubleClick: () => useDoubleClick,
  useGeolocation: () => useGeolocation,
  useImpressionRef: () => useImpressionRef,
  useInputState: () => useInputState,
  useIntersectionObserver: () => useIntersectionObserver,
  useInterval: () => useInterval,
  useIsomorphicLayoutEffect: () => useIsomorphicLayoutEffect,
  useLoading: () => useLoading,
  useLongPress: () => useLongPress,
  useMap: () => useMap,
  useOutsideClickEffect: () => useOutsideClickEffect,
  usePreservedCallback: () => usePreservedCallback,
  usePreservedReference: () => usePreservedReference,
  usePrevious: () => usePrevious,
  useRefEffect: () => useRefEffect,
  useStorageState: () => useStorageState,
  useThrottle: () => useThrottle,
  useTimeout: () => useTimeout,
  useToggle: () => useToggle,
  useVisibilityEvent: () => useVisibilityEvent
});
module.exports = __toCommonJS(index_exports);

// src/components/ImpressionArea/ImpressionArea.tsx
var import_react7 = require("react");

// src/hooks/useImpressionRef/useImpressionRef.ts
var import_react6 = require("react");

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

// src/hooks/useIntersectionObserver/useIntersectionObserver.ts
var import_react4 = require("react");

// src/hooks/useRefEffect/useRefEffect.ts
var import_react3 = require("react");
function useRefEffect(callback, deps) {
  const preservedCallback = usePreservedCallback(callback);
  const cleanupCallbackRef = (0, import_react3.useRef)(() => {
  });
  const effect = (0, import_react3.useCallback)(
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
  const observer = (0, import_react4.useMemo)(() => {
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

// src/hooks/useVisibilityEvent/useVisibilityEvent.ts
var import_react5 = require("react");
function useVisibilityEvent(callback, options = {}) {
  const handleVisibilityChange = (0, import_react5.useCallback)(() => {
    callback(document.visibilityState);
  }, [callback]);
  (0, import_react5.useEffect)(() => {
    if (options?.immediate ?? false) {
      handleVisibilityChange();
    }
    document.addEventListener("visibilitychange", handleVisibilityChange);
    return () => {
      document.removeEventListener("visibilitychange", handleVisibilityChange);
    };
  }, [handleVisibilityChange, options?.immediate]);
}

// src/hooks/useImpressionRef/useImpressionRef.ts
function useImpressionRef({
  onImpressionStart = () => {
  },
  onImpressionEnd = () => {
  },
  rootMargin,
  areaThreshold = 0,
  timeThreshold = 0
}) {
  const impressionStartHandler = usePreservedCallback(onImpressionStart);
  const impressionEndHandler = usePreservedCallback(onImpressionEnd);
  const isIntersectingRef = (0, import_react6.useRef)(false);
  const impressionEventHandler = useDebouncedCallback({
    timeThreshold,
    onChange: (impressed) => {
      (impressed ? impressionStartHandler : impressionEndHandler)();
    },
    leading: true
  });
  useVisibilityEvent((documentVisible) => {
    if (!isIntersectingRef.current) {
      return;
    }
    impressionEventHandler(documentVisible === "visible");
  });
  return useIntersectionObserver(
    (entry) => {
      if (document.visibilityState === "hidden") {
        return;
      }
      const currentRatio = entry.intersectionRatio;
      const isIntersecting = areaThreshold === 0 ? entry.isIntersecting : currentRatio >= areaThreshold;
      isIntersectingRef.current = isIntersecting;
      impressionEventHandler(isIntersecting);
    },
    { rootMargin, threshold: areaThreshold }
  );
}

// src/utils/mergeRefs/mergeRefs.ts
function mergeRefs(...refs) {
  return (value) => {
    for (const ref of refs) {
      if (ref == null) {
        continue;
      }
      if (typeof ref === "function") {
        ref(value);
        continue;
      }
      ref.current = value;
    }
  };
}

// src/components/ImpressionArea/ImpressionArea.tsx
var import_jsx_runtime = require("react/jsx-runtime");
var ImpressionArea = (0, import_react7.forwardRef)(ImpressionAreaImpl);
function ImpressionAreaImpl({ as, rootMargin, areaThreshold, timeThreshold, onImpressionStart, onImpressionEnd, ...props }, ref) {
  const Component = as ?? "div";
  const impressionRef = useImpressionRef({
    onImpressionStart,
    onImpressionEnd,
    areaThreshold,
    timeThreshold,
    rootMargin
  });
  return /* @__PURE__ */ (0, import_jsx_runtime.jsx)(Component, { ref: mergeRefs(ref, impressionRef), ...props });
}
Object.assign(ImpressionArea, {
  displayName: "ImpressionArea"
});

// src/components/Separated/Separated.tsx
var import_react8 = require("react");
var import_jsx_runtime2 = require("react/jsx-runtime");
function Separated({ children, by: separator }) {
  const childrenArray = import_react8.Children.toArray(children).filter(import_react8.isValidElement);
  return /* @__PURE__ */ (0, import_jsx_runtime2.jsx)(import_jsx_runtime2.Fragment, { children: childrenArray.map((child, i, { length }) => /* @__PURE__ */ (0, import_jsx_runtime2.jsxs)(import_react8.Fragment, { children: [
    child,
    i + 1 !== length && separator
  ] }, i)) });
}

// src/components/SwitchCase/SwitchCase.tsx
function SwitchCase({ value, caseBy, defaultComponent = () => null }) {
  const stringifiedValue = String(value);
  return (caseBy[stringifiedValue] ?? defaultComponent)();
}

// src/hooks/useAsyncEffect/useAsyncEffect.ts
var import_react9 = require("react");
function useAsyncEffect(effect, deps) {
  (0, import_react9.useEffect)(() => {
    let cleanup;
    effect().then((result) => {
      cleanup = result;
    });
    return () => {
      cleanup?.();
    };
  }, deps);
}

// src/hooks/useBooleanState/useBooleanState.ts
var import_react10 = require("react");
function useBooleanState(defaultValue = false) {
  const [bool, setBool] = (0, import_react10.useState)(defaultValue);
  const setTrue = (0, import_react10.useCallback)(() => {
    setBool(true);
  }, []);
  const setFalse = (0, import_react10.useCallback)(() => {
    setBool(false);
  }, []);
  const toggle2 = (0, import_react10.useCallback)(() => {
    setBool((prevBool) => !prevBool);
  }, []);
  return [bool, setTrue, setFalse, toggle2];
}

// src/hooks/useCallbackOncePerRender/useCallbackOncePerRender.ts
var import_react11 = require("react");
function useCallbackOncePerRender(callback, deps) {
  const hasFired = (0, import_react11.useRef)(false);
  (0, import_react11.useEffect)(() => {
    hasFired.current = false;
  }, deps);
  return usePreservedCallback((...args) => {
    if (hasFired.current) {
      return;
    }
    callback(...args);
    hasFired.current = true;
  });
}

// src/hooks/useConditionalEffect/useConditionalEffect.ts
var import_react12 = require("react");
function useConditionalEffect(effect, deps, condition) {
  const prevDepsRef = (0, import_react12.useRef)(void 0);
  const memoizedCondition = (0, import_react12.useCallback)(condition, deps);
  if (deps.length === 0) {
    console.warn(
      "useConditionalEffect received an empty dependency array. This may indicate missing dependencies and could lead to unexpected behavior."
    );
  }
  const shouldRun = memoizedCondition(prevDepsRef.current, deps);
  (0, import_react12.useEffect)(() => {
    if (shouldRun) {
      const cleanup = effect();
      prevDepsRef.current = deps;
      return cleanup;
    }
    prevDepsRef.current = deps;
  }, deps);
}

// src/hooks/useControlledState/useControlledState.ts
var import_react13 = require("react");
function useControlledState({
  value: valueProp,
  defaultValue,
  onChange,
  equalityFn = Object.is
}) {
  const [uncontrolledState, setUncontrolledState] = (0, import_react13.useState)(defaultValue);
  const controlled = valueProp !== void 0;
  const value = controlled ? valueProp : uncontrolledState;
  const setValue = (0, import_react13.useCallback)(
    (next) => {
      const nextValue = isSetStateAction(next) ? next(value) : next;
      if (equalityFn(value, nextValue) === true) return;
      if (controlled === false) setUncontrolledState(nextValue);
      if (controlled === true && nextValue === void 0) setUncontrolledState(nextValue);
      onChange?.(nextValue);
    },
    [controlled, onChange, equalityFn, value]
  );
  return [value, setValue];
}
function isSetStateAction(next) {
  return typeof next === "function";
}

// src/hooks/useCounter/useCounter.ts
var import_react14 = require("react");
function useCounter(initialValue = 0, { min, max, step = 1 } = {}) {
  const validateValue = (value) => {
    let validatedValue = value;
    if (min !== void 0 && validatedValue < min) {
      validatedValue = min;
    }
    if (max !== void 0 && validatedValue > max) {
      validatedValue = max;
    }
    return validatedValue;
  };
  const [count, setCountState] = (0, import_react14.useState)(() => validateValue(initialValue));
  const validateValueMemoized = (0, import_react14.useCallback)(validateValue, [min, max]);
  const setCount = (0, import_react14.useCallback)(
    (value) => {
      setCountState((prev) => {
        const nextValue = typeof value === "function" ? value(prev) : value;
        return validateValueMemoized(nextValue);
      });
    },
    [validateValueMemoized]
  );
  const increment = (0, import_react14.useCallback)(() => {
    setCount((prev) => prev + step);
  }, [setCount, step]);
  const decrement = (0, import_react14.useCallback)(() => {
    setCount((prev) => prev - step);
  }, [setCount, step]);
  const reset = (0, import_react14.useCallback)(() => {
    setCount(initialValue);
  }, [setCount, initialValue]);
  return {
    count,
    increment,
    decrement,
    reset,
    setCount
  };
}

// src/hooks/useDebounce/useDebounce.ts
var import_react15 = require("react");
var import_react16 = require("react");
function useDebounce(callback, wait, options = {}) {
  const preservedCallback = usePreservedCallback(callback);
  const { leading = false, trailing = true } = options;
  const edges = (0, import_react16.useMemo)(() => {
    const _edges = [];
    if (leading) {
      _edges.push("leading");
    }
    if (trailing) {
      _edges.push("trailing");
    }
    return _edges;
  }, [leading, trailing]);
  const debounced = (0, import_react16.useMemo)(() => {
    return debounce(preservedCallback, wait, { edges });
  }, [preservedCallback, wait, edges]);
  (0, import_react15.useEffect)(() => {
    return () => {
      debounced.cancel();
    };
  }, [debounced]);
  return debounced;
}

// src/hooks/useDoubleClick/useDoubleClick.ts
var import_react17 = require("react");
function useDoubleClick({
  delay = 250,
  click,
  doubleClick
}) {
  const clickTimeout = (0, import_react17.useRef)(null);
  const clearClickTimeout = usePreservedCallback(() => {
    if (clickTimeout.current != null) {
      window.clearTimeout(clickTimeout.current);
      clickTimeout.current = null;
    }
  });
  (0, import_react17.useEffect)(() => () => clearClickTimeout(), [clearClickTimeout]);
  const handleEvent = (0, import_react17.useCallback)(
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

// src/hooks/useGeolocation/useGeolocation.ts
var import_react18 = require("react");
var CustomGeoLocationError = class extends Error {
  code;
  constructor({ code, message }) {
    super(message);
    this.name = "CustomGeoLocationError";
    this.code = code;
  }
};
var GeolocationMountBehavior = {
  GET: "get",
  WATCH: "watch"
};
function useGeolocation(options) {
  const [state, setState] = (0, import_react18.useState)({
    loading: !!options?.mountBehavior,
    error: null,
    data: null
  });
  const [isTracking, setIsTracking] = (0, import_react18.useState)(false);
  const watchIdRef = (0, import_react18.useRef)(null);
  const checkGeolocationSupport = (0, import_react18.useCallback)(() => {
    if (typeof window === "undefined" || navigator.geolocation === void 0) {
      setState((prev) => ({
        ...prev,
        loading: false,
        error: new CustomGeoLocationError({
          code: 0,
          message: "Geolocation is not supported by this environment."
        })
      }));
      return false;
    }
    return true;
  }, []);
  const handleSuccess = (0, import_react18.useCallback)((position) => {
    const { coords } = position;
    setState((prev) => ({
      ...prev,
      loading: false,
      error: null,
      data: {
        latitude: coords.latitude,
        longitude: coords.longitude,
        accuracy: coords.accuracy,
        altitude: coords.altitude,
        altitudeAccuracy: coords.altitudeAccuracy,
        heading: coords.heading,
        speed: coords.speed,
        timestamp: position.timestamp
      }
    }));
  }, []);
  const handleError = (0, import_react18.useCallback)((error) => {
    const { code, message } = error;
    setState((prev) => ({
      ...prev,
      loading: false,
      error: new CustomGeoLocationError({ code, message })
    }));
  }, []);
  const getGeolocationOptions = (0, import_react18.useCallback)(
    () => ({
      enableHighAccuracy: options?.enableHighAccuracy,
      maximumAge: options?.maximumAge,
      timeout: options?.timeout
    }),
    [options?.enableHighAccuracy, options?.maximumAge, options?.timeout]
  );
  const getCurrentPosition = (0, import_react18.useCallback)(() => {
    if (!checkGeolocationSupport()) {
      return;
    }
    setState((prev) => ({ ...prev, loading: true }));
    navigator.geolocation.getCurrentPosition(handleSuccess, handleError, getGeolocationOptions());
  }, [handleSuccess, handleError, getGeolocationOptions, checkGeolocationSupport]);
  const startTracking = (0, import_react18.useCallback)(() => {
    if (!checkGeolocationSupport()) {
      return;
    }
    if (watchIdRef.current !== null) {
      navigator.geolocation.clearWatch(watchIdRef.current);
    }
    setState((prev) => ({ ...prev, loading: true }));
    watchIdRef.current = navigator.geolocation.watchPosition(
      (position) => {
        setIsTracking(true);
        handleSuccess(position);
      },
      handleError,
      getGeolocationOptions()
    );
  }, [handleSuccess, handleError, getGeolocationOptions, checkGeolocationSupport]);
  const stopTracking = (0, import_react18.useCallback)(() => {
    if (watchIdRef.current === null) {
      return;
    }
    navigator.geolocation.clearWatch(watchIdRef.current);
    watchIdRef.current = null;
    setIsTracking(false);
  }, []);
  (0, import_react18.useEffect)(() => {
    if (options?.mountBehavior === GeolocationMountBehavior.WATCH) {
      startTracking();
    } else if (options?.mountBehavior === GeolocationMountBehavior.GET) {
      getCurrentPosition();
    }
    return () => {
      if (watchIdRef.current !== null) {
        navigator.geolocation.clearWatch(watchIdRef.current);
        watchIdRef.current = null;
      }
    };
  }, [options?.mountBehavior, getCurrentPosition, startTracking]);
  return {
    ...state,
    getCurrentPosition,
    startTracking,
    stopTracking,
    isTracking
  };
}

// src/hooks/useInputState/useInputState.ts
var import_react19 = require("react");
function useInputState(initialValue = "", transformValue = echo) {
  const [value, setValue] = (0, import_react19.useState)(initialValue);
  const handleValueChange = (0, import_react19.useCallback)(
    ({ target: { value: value2 } }) => {
      setValue(transformValue(value2));
    },
    [transformValue]
  );
  return [value, handleValueChange];
}
function echo(v) {
  return v;
}

// src/hooks/useInterval/useInterval.ts
var import_react20 = require("react");
function useInterval(callback, options) {
  const delay = typeof options === "number" ? options : options.delay;
  const immediate = typeof options === "number" ? false : options.immediate;
  const enabled = typeof options === "number" ? true : options.enabled ?? true;
  const preservedCallback = usePreservedCallback(callback);
  (0, import_react20.useEffect)(() => {
    if (immediate === true && enabled) {
      preservedCallback();
    }
  }, [immediate, preservedCallback, enabled]);
  (0, import_react20.useEffect)(() => {
    if (!enabled) {
      return;
    }
    const id = window.setInterval(preservedCallback, delay);
    return () => window.clearInterval(id);
  }, [delay, preservedCallback, enabled]);
}

// src/hooks/useIsomorphicLayoutEffect/useIsomorphicLayoutEffect.ts
var import_react21 = require("react");
var isServer = typeof window === "undefined";
var useIsomorphicLayoutEffect = isServer ? import_react21.useEffect : import_react21.useLayoutEffect;

// src/hooks/useLoading/useLoading.ts
var import_react22 = require("react");
function useLoading() {
  const [loading, setLoading] = (0, import_react22.useState)(false);
  const ref = useIsMountedRef();
  const startTransition = (0, import_react22.useCallback)(
    async (promise) => {
      try {
        setLoading(true);
        const data = await promise;
        return data;
      } finally {
        if (ref.isMounted) {
          setLoading(false);
        }
      }
    },
    [ref.isMounted]
  );
  return (0, import_react22.useMemo)(() => [loading, startTransition], [loading, startTransition]);
}
function useIsMountedRef() {
  const ref = (0, import_react22.useRef)({ isMounted: true }).current;
  (0, import_react22.useEffect)(() => {
    ref.isMounted = true;
    return () => {
      ref.isMounted = false;
    };
  }, [ref]);
  return ref;
}

// src/hooks/useLongPress/useLongPress.ts
var import_react23 = require("react");
function useLongPress(onLongPress, { delay = 500, moveThreshold, onClick, onLongPressEnd } = {}) {
  const timeoutRef = (0, import_react23.useRef)(null);
  const isLongPressActiveRef = (0, import_react23.useRef)(false);
  const initialPositionRef = (0, import_react23.useRef)({ x: 0, y: 0 });
  const preservedOnLongPress = usePreservedCallback(onLongPress);
  const preservedOnClick = usePreservedCallback(onClick || (() => {
  }));
  const preservedOnLongPressEnd = usePreservedCallback(onLongPressEnd || (() => {
  }));
  const hasThreshold = moveThreshold?.x !== void 0 || moveThreshold?.y !== void 0;
  const getClientPosition = (0, import_react23.useCallback)((event) => {
    if ("touches" in event.nativeEvent) {
      const touch = event.nativeEvent.touches[0];
      return { x: touch.clientX, y: touch.clientY };
    }
    return {
      x: event.nativeEvent.clientX,
      y: event.nativeEvent.clientY
    };
  }, []);
  const isMovedBeyondThreshold = (0, import_react23.useCallback)(
    (event) => {
      const { x, y } = getClientPosition(event);
      const deltaX = Math.abs(x - initialPositionRef.current.x);
      const deltaY = Math.abs(y - initialPositionRef.current.y);
      return moveThreshold?.x !== void 0 && deltaX > moveThreshold.x || moveThreshold?.y !== void 0 && deltaY > moveThreshold.y;
    },
    [getClientPosition, moveThreshold]
  );
  const cancelLongPress = (0, import_react23.useCallback)(() => {
    if (timeoutRef.current !== null) {
      window.clearTimeout(timeoutRef.current);
      timeoutRef.current = null;
    }
  }, []);
  const handlePressStart = (0, import_react23.useCallback)(
    (event) => {
      cancelLongPress();
      const position = getClientPosition(event);
      initialPositionRef.current = position;
      isLongPressActiveRef.current = false;
      timeoutRef.current = window.setTimeout(() => {
        isLongPressActiveRef.current = true;
        preservedOnLongPress(event);
      }, delay);
    },
    [cancelLongPress, delay, getClientPosition, preservedOnLongPress]
  );
  const handlePressEnd = (0, import_react23.useCallback)(
    (event) => {
      if (isLongPressActiveRef.current) {
        preservedOnLongPressEnd(event);
      } else if (timeoutRef.current !== null) {
        preservedOnClick(event);
      }
      cancelLongPress();
      isLongPressActiveRef.current = false;
    },
    [cancelLongPress, preservedOnClick, preservedOnLongPressEnd]
  );
  const handlePressMove = (0, import_react23.useCallback)(
    (event) => {
      if (timeoutRef.current !== null && isMovedBeyondThreshold(event)) {
        cancelLongPress();
      }
    },
    [cancelLongPress, isMovedBeyondThreshold]
  );
  return {
    onMouseDown: handlePressStart,
    onMouseUp: handlePressEnd,
    onMouseLeave: cancelLongPress,
    onTouchStart: handlePressStart,
    onTouchEnd: handlePressEnd,
    ...hasThreshold ? { onTouchMove: handlePressMove, onMouseMove: handlePressMove } : {}
  };
}

// src/hooks/useMap/useMap.ts
var import_react25 = require("react");

// src/hooks/usePreservedReference/usePreservedReference.ts
var import_react24 = require("react");
function usePreservedReference(value, areValuesEqual = areDeeplyEqual) {
  const ref = (0, import_react24.useRef)(value);
  return (0, import_react24.useMemo)(() => {
    if (!areValuesEqual(ref.current, value)) {
      ref.current = value;
    }
    return ref.current;
  }, [areValuesEqual, value]);
}
function areDeeplyEqual(x, y) {
  return JSON.stringify(x) === JSON.stringify(y);
}

// src/hooks/useMap/useMap.ts
function useMap(initialState = /* @__PURE__ */ new Map()) {
  const [map, setMap] = (0, import_react25.useState)(() => new Map(initialState));
  const preservedInitialState = usePreservedReference(initialState);
  const set = (0, import_react25.useCallback)((key, value) => {
    setMap((prev) => {
      const nextMap = new Map(prev);
      nextMap.set(key, value);
      return nextMap;
    });
  }, []);
  const setAll = (0, import_react25.useCallback)((entries) => {
    setMap(() => new Map(entries));
  }, []);
  const remove = (0, import_react25.useCallback)((key) => {
    setMap((prev) => {
      const nextMap = new Map(prev);
      nextMap.delete(key);
      return nextMap;
    });
  }, []);
  const reset = (0, import_react25.useCallback)(() => {
    setMap(() => new Map(preservedInitialState));
  }, [preservedInitialState]);
  const actions = (0, import_react25.useMemo)(() => {
    return { set, setAll, remove, reset };
  }, [set, setAll, remove, reset]);
  return [map, actions];
}

// src/hooks/useOutsideClickEffect/useOutsideClickEffect.ts
var import_react26 = require("react");
function useOutsideClickEffect(container, callback) {
  const containers = (0, import_react26.useRef)([]);
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
  (0, import_react26.useEffect)(() => {
    containers.current = [container].flat(1).filter((item) => item != null);
  }, [container]);
  (0, import_react26.useEffect)(() => {
    document.addEventListener("click", handleDocumentClick);
    return () => {
      document.removeEventListener("click", handleDocumentClick);
    };
  }, [handleDocumentClick]);
}

// src/hooks/usePrevious/usePrevious.ts
var import_react27 = require("react");
var strictEquals = (prev, next) => prev === next;
function usePrevious(state, compare = strictEquals) {
  const prevRef = (0, import_react27.useRef)(state);
  const currentRef = (0, import_react27.useRef)(state);
  const isFirstRender = (0, import_react27.useRef)(true);
  if (isFirstRender.current) {
    isFirstRender.current = false;
    return prevRef.current;
  }
  if (!compare(currentRef.current, state)) {
    prevRef.current = currentRef.current;
    currentRef.current = state;
  }
  return prevRef.current;
}

// src/hooks/useStorageState/useStorageState.ts
var import_react28 = require("react");

// src/hooks/useStorageState/storage.ts
var MemoStorage = class {
  storage = /* @__PURE__ */ new Map();
  get(key) {
    return this.storage.get(key) ?? null;
  }
  set(key, value) {
    this.storage.set(key, value);
  }
  remove(key) {
    this.storage.delete(key);
  }
  clear() {
    this.storage.clear();
  }
};
var LocalStorage = class {
  static canUse() {
    const TEST_KEY = generateTestKey();
    try {
      localStorage.setItem(TEST_KEY, "test");
      localStorage.removeItem(TEST_KEY);
      return true;
    } catch {
      return false;
    }
  }
  get(key) {
    return localStorage.getItem(key);
  }
  set(key, value) {
    localStorage.setItem(key, value);
  }
  remove(key) {
    localStorage.removeItem(key);
  }
  clear() {
    localStorage.clear();
  }
};
var SessionStorage = class {
  static canUse() {
    const TEST_KEY = generateTestKey();
    try {
      sessionStorage.setItem(TEST_KEY, "test");
      sessionStorage.removeItem(TEST_KEY);
      return true;
    } catch {
      return false;
    }
  }
  get(key) {
    return sessionStorage.getItem(key);
  }
  set(key, value) {
    sessionStorage.setItem(key, value);
  }
  remove(key) {
    sessionStorage.removeItem(key);
  }
  clear() {
    sessionStorage.clear();
  }
};
function generateTestKey() {
  return new Array(4).fill(null).map(() => Math.random().toString(36).slice(2)).join("");
}
function generateStorage() {
  if (LocalStorage.canUse()) {
    return new LocalStorage();
  }
  return new MemoStorage();
}
function generateSessionStorage() {
  if (SessionStorage.canUse()) {
    return new SessionStorage();
  }
  return new MemoStorage();
}
var safeLocalStorage = generateStorage();
var safeSessionStorage = generateSessionStorage();

// src/hooks/useStorageState/useStorageState.ts
var listeners = /* @__PURE__ */ new Set();
var emitListeners = () => {
  listeners.forEach((listener) => listener());
};
function isPlainObject(value) {
  if (typeof value !== "object") {
    return false;
  }
  const proto = Object.getPrototypeOf(value);
  const hasObjectPrototype = proto === Object.prototype;
  if (!hasObjectPrototype) {
    return false;
  }
  return Object.prototype.toString.call(value) === "[object Object]";
}
var ensureSerializable = (value) => {
  if (value[0] != null && !["string", "number", "boolean"].includes(typeof value[0]) && !(isPlainObject(value[0]) || Array.isArray(value[0]))) {
    throw new Error("Received a non-serializable value");
  }
  return value;
};
function useStorageState(key, {
  storage = safeLocalStorage,
  defaultValue,
  ...options
} = {}) {
  const serializedDefaultValue = defaultValue;
  const cache = (0, import_react28.useRef)({
    data: null,
    parsed: serializedDefaultValue
  });
  const getSnapshot = (0, import_react28.useCallback)(() => {
    const deserializer = "deserializer" in options ? options.deserializer : JSON.parse;
    const data = storage.get(key);
    if (data !== cache.current.data) {
      try {
        cache.current.parsed = data != null ? deserializer(data) : defaultValue;
      } catch {
        cache.current.parsed = serializedDefaultValue;
      }
      cache.current.data = data;
    }
    return cache.current.parsed;
  }, [defaultValue, key, storage]);
  const storageState = (0, import_react28.useSyncExternalStore)(
    (onStoreChange) => {
      listeners.add(onStoreChange);
      const handler = (event) => {
        if (event.key === key) {
          onStoreChange();
        }
      };
      window.addEventListener("storage", handler);
      return () => {
        listeners.delete(onStoreChange);
        window.removeEventListener("storage", handler);
      };
    },
    () => getSnapshot(),
    () => serializedDefaultValue
  );
  const setStorageState = (0, import_react28.useCallback)(
    (value) => {
      const serializer = "serializer" in options ? options.serializer : JSON.stringify;
      const nextValue = typeof value === "function" ? value(getSnapshot()) : value;
      if (nextValue == null) {
        storage.remove(key);
      } else {
        storage.set(key, serializer(nextValue));
      }
      emitListeners();
    },
    [getSnapshot, key, storage]
  );
  const refreshStorageState = (0, import_react28.useCallback)(() => {
    setStorageState(getSnapshot());
  }, [storage, getSnapshot, setStorageState]);
  return ensureSerializable([storageState, setStorageState, refreshStorageState]);
}

// src/hooks/useThrottle/useThrottle.ts
var import_react29 = require("react");

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
  const throttledCallback = (0, import_react29.useMemo)(
    () => throttle(preservedCallback, wait, preservedOptions),
    [preservedOptions, preservedCallback, wait]
  );
  (0, import_react29.useEffect)(() => {
    return () => {
      throttledCallback.cancel();
    };
  }, [throttledCallback]);
  return throttledCallback;
}

// src/hooks/useTimeout/useTimeout.ts
var import_react30 = require("react");
function useTimeout(callback, delay = 0) {
  const preservedCallback = usePreservedCallback(callback);
  (0, import_react30.useEffect)(() => {
    const timeoutId = window.setTimeout(preservedCallback, delay);
    return () => window.clearTimeout(timeoutId);
  }, [delay, preservedCallback]);
}

// src/hooks/useToggle/useToggle.ts
var import_react31 = require("react");
function useToggle(initialValue = false) {
  return (0, import_react31.useReducer)(toggle, initialValue);
}
var toggle = (state) => !state;

// src/utils/buildContext/buildContext.tsx
var import_react32 = require("react");
var import_jsx_runtime3 = require("react/jsx-runtime");
function buildContext(contextName, defaultContextValues) {
  const Context = (0, import_react32.createContext)(defaultContextValues ?? void 0);
  function Provider({ children, ...contextValues }) {
    const value = (0, import_react32.useMemo)(
      () => Object.keys(contextValues).length > 0 ? contextValues : null,
      // eslint-disable-next-line react-hooks/exhaustive-deps
      [...Object.values(contextValues)]
    );
    return /* @__PURE__ */ (0, import_jsx_runtime3.jsx)(Context.Provider, { value, children });
  }
  function useInnerContext() {
    const context = (0, import_react32.useContext)(Context);
    if (context != null) {
      return context;
    }
    if (defaultContextValues != null) {
      return defaultContextValues;
    }
    throw new Error(`\`${contextName}Context\` must be used within \`${contextName}Provider\``);
  }
  return [Provider, useInnerContext];
}

// src/utils/mergeProps/mergeProps.ts
function mergeProps(...props) {
  return props.reduce(pushProp, {});
}
function pushProp(prev, curr) {
  for (const key in curr) {
    if (curr[key] === void 0) continue;
    switch (key) {
      case "className": {
        prev[key] = [prev[key], curr[key]].join(" ").trim();
        break;
      }
      case "style": {
        prev[key] = mergeStyle(prev[key], curr[key]);
        break;
      }
      default: {
        const mergedFunction = mergeFunction(prev[key], curr[key]);
        if (mergedFunction) {
          prev[key] = mergedFunction;
        } else if (curr[key] !== void 0) {
          prev[key] = curr[key];
        }
      }
    }
  }
  return prev;
}
function mergeStyle(a, b) {
  if (a == null) return b;
  return { ...a, ...b };
}
function mergeFunction(a, b) {
  if (typeof a === "function" && typeof b === "function") {
    return (...args) => {
      a(...args);
      b(...args);
    };
  }
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  ImpressionArea,
  Separated,
  SwitchCase,
  buildContext,
  mergeProps,
  mergeRefs,
  useAsyncEffect,
  useBooleanState,
  useCallbackOncePerRender,
  useConditionalEffect,
  useControlledState,
  useCounter,
  useDebounce,
  useDebouncedCallback,
  useDoubleClick,
  useGeolocation,
  useImpressionRef,
  useInputState,
  useIntersectionObserver,
  useInterval,
  useIsomorphicLayoutEffect,
  useLoading,
  useLongPress,
  useMap,
  useOutsideClickEffect,
  usePreservedCallback,
  usePreservedReference,
  usePrevious,
  useRefEffect,
  useStorageState,
  useThrottle,
  useTimeout,
  useToggle,
  useVisibilityEvent
});
