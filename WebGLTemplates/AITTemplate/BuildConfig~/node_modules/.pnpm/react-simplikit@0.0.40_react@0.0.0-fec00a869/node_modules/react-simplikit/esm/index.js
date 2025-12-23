// src/components/ImpressionArea/ImpressionArea.tsx
import { forwardRef } from "react";

// src/hooks/useImpressionRef/useImpressionRef.ts
import { useRef as useRef4 } from "react";

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

// src/hooks/useIntersectionObserver/useIntersectionObserver.ts
import { useMemo as useMemo2 } from "react";

// src/hooks/useRefEffect/useRefEffect.ts
import { useCallback as useCallback3, useRef as useRef3 } from "react";
function useRefEffect(callback, deps) {
  const preservedCallback = usePreservedCallback(callback);
  const cleanupCallbackRef = useRef3(() => {
  });
  const effect = useCallback3(
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
  const observer = useMemo2(() => {
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
import { useCallback as useCallback4, useEffect as useEffect3 } from "react";
function useVisibilityEvent(callback, options = {}) {
  const handleVisibilityChange = useCallback4(() => {
    callback(document.visibilityState);
  }, [callback]);
  useEffect3(() => {
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
  const isIntersectingRef = useRef4(false);
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
import { jsx } from "react/jsx-runtime";
var ImpressionArea = forwardRef(ImpressionAreaImpl);
function ImpressionAreaImpl({ as, rootMargin, areaThreshold, timeThreshold, onImpressionStart, onImpressionEnd, ...props }, ref) {
  const Component = as ?? "div";
  const impressionRef = useImpressionRef({
    onImpressionStart,
    onImpressionEnd,
    areaThreshold,
    timeThreshold,
    rootMargin
  });
  return /* @__PURE__ */ jsx(Component, { ref: mergeRefs(ref, impressionRef), ...props });
}
Object.assign(ImpressionArea, {
  displayName: "ImpressionArea"
});

// src/components/Separated/Separated.tsx
import { Children, Fragment, isValidElement } from "react";
import { Fragment as Fragment2, jsx as jsx2, jsxs } from "react/jsx-runtime";
function Separated({ children, by: separator }) {
  const childrenArray = Children.toArray(children).filter(isValidElement);
  return /* @__PURE__ */ jsx2(Fragment2, { children: childrenArray.map((child, i, { length }) => /* @__PURE__ */ jsxs(Fragment, { children: [
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
import { useEffect as useEffect4 } from "react";
function useAsyncEffect(effect, deps) {
  useEffect4(() => {
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
import { useCallback as useCallback5, useState } from "react";
function useBooleanState(defaultValue = false) {
  const [bool, setBool] = useState(defaultValue);
  const setTrue = useCallback5(() => {
    setBool(true);
  }, []);
  const setFalse = useCallback5(() => {
    setBool(false);
  }, []);
  const toggle2 = useCallback5(() => {
    setBool((prevBool) => !prevBool);
  }, []);
  return [bool, setTrue, setFalse, toggle2];
}

// src/hooks/useCallbackOncePerRender/useCallbackOncePerRender.ts
import { useEffect as useEffect5, useRef as useRef5 } from "react";
function useCallbackOncePerRender(callback, deps) {
  const hasFired = useRef5(false);
  useEffect5(() => {
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
import { useCallback as useCallback6, useEffect as useEffect6, useRef as useRef6 } from "react";
function useConditionalEffect(effect, deps, condition) {
  const prevDepsRef = useRef6(void 0);
  const memoizedCondition = useCallback6(condition, deps);
  if (deps.length === 0) {
    console.warn(
      "useConditionalEffect received an empty dependency array. This may indicate missing dependencies and could lead to unexpected behavior."
    );
  }
  const shouldRun = memoizedCondition(prevDepsRef.current, deps);
  useEffect6(() => {
    if (shouldRun) {
      const cleanup = effect();
      prevDepsRef.current = deps;
      return cleanup;
    }
    prevDepsRef.current = deps;
  }, deps);
}

// src/hooks/useControlledState/useControlledState.ts
import { useCallback as useCallback7, useState as useState2 } from "react";
function useControlledState({
  value: valueProp,
  defaultValue,
  onChange,
  equalityFn = Object.is
}) {
  const [uncontrolledState, setUncontrolledState] = useState2(defaultValue);
  const controlled = valueProp !== void 0;
  const value = controlled ? valueProp : uncontrolledState;
  const setValue = useCallback7(
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
import { useCallback as useCallback8, useState as useState3 } from "react";
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
  const [count, setCountState] = useState3(() => validateValue(initialValue));
  const validateValueMemoized = useCallback8(validateValue, [min, max]);
  const setCount = useCallback8(
    (value) => {
      setCountState((prev) => {
        const nextValue = typeof value === "function" ? value(prev) : value;
        return validateValueMemoized(nextValue);
      });
    },
    [validateValueMemoized]
  );
  const increment = useCallback8(() => {
    setCount((prev) => prev + step);
  }, [setCount, step]);
  const decrement = useCallback8(() => {
    setCount((prev) => prev - step);
  }, [setCount, step]);
  const reset = useCallback8(() => {
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
import { useEffect as useEffect7 } from "react";
import { useMemo as useMemo3 } from "react";
function useDebounce(callback, wait, options = {}) {
  const preservedCallback = usePreservedCallback(callback);
  const { leading = false, trailing = true } = options;
  const edges = useMemo3(() => {
    const _edges = [];
    if (leading) {
      _edges.push("leading");
    }
    if (trailing) {
      _edges.push("trailing");
    }
    return _edges;
  }, [leading, trailing]);
  const debounced = useMemo3(() => {
    return debounce(preservedCallback, wait, { edges });
  }, [preservedCallback, wait, edges]);
  useEffect7(() => {
    return () => {
      debounced.cancel();
    };
  }, [debounced]);
  return debounced;
}

// src/hooks/useDoubleClick/useDoubleClick.ts
import { useCallback as useCallback9, useEffect as useEffect8, useRef as useRef7 } from "react";
function useDoubleClick({
  delay = 250,
  click,
  doubleClick
}) {
  const clickTimeout = useRef7(null);
  const clearClickTimeout = usePreservedCallback(() => {
    if (clickTimeout.current != null) {
      window.clearTimeout(clickTimeout.current);
      clickTimeout.current = null;
    }
  });
  useEffect8(() => () => clearClickTimeout(), [clearClickTimeout]);
  const handleEvent = useCallback9(
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
import { useCallback as useCallback10, useEffect as useEffect9, useRef as useRef8, useState as useState4 } from "react";
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
  const [state, setState] = useState4({
    loading: !!options?.mountBehavior,
    error: null,
    data: null
  });
  const [isTracking, setIsTracking] = useState4(false);
  const watchIdRef = useRef8(null);
  const checkGeolocationSupport = useCallback10(() => {
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
  const handleSuccess = useCallback10((position) => {
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
  const handleError = useCallback10((error) => {
    const { code, message } = error;
    setState((prev) => ({
      ...prev,
      loading: false,
      error: new CustomGeoLocationError({ code, message })
    }));
  }, []);
  const getGeolocationOptions = useCallback10(
    () => ({
      enableHighAccuracy: options?.enableHighAccuracy,
      maximumAge: options?.maximumAge,
      timeout: options?.timeout
    }),
    [options?.enableHighAccuracy, options?.maximumAge, options?.timeout]
  );
  const getCurrentPosition = useCallback10(() => {
    if (!checkGeolocationSupport()) {
      return;
    }
    setState((prev) => ({ ...prev, loading: true }));
    navigator.geolocation.getCurrentPosition(handleSuccess, handleError, getGeolocationOptions());
  }, [handleSuccess, handleError, getGeolocationOptions, checkGeolocationSupport]);
  const startTracking = useCallback10(() => {
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
  const stopTracking = useCallback10(() => {
    if (watchIdRef.current === null) {
      return;
    }
    navigator.geolocation.clearWatch(watchIdRef.current);
    watchIdRef.current = null;
    setIsTracking(false);
  }, []);
  useEffect9(() => {
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
import { useCallback as useCallback11, useState as useState5 } from "react";
function useInputState(initialValue = "", transformValue = echo) {
  const [value, setValue] = useState5(initialValue);
  const handleValueChange = useCallback11(
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
import { useEffect as useEffect10 } from "react";
function useInterval(callback, options) {
  const delay = typeof options === "number" ? options : options.delay;
  const immediate = typeof options === "number" ? false : options.immediate;
  const enabled = typeof options === "number" ? true : options.enabled ?? true;
  const preservedCallback = usePreservedCallback(callback);
  useEffect10(() => {
    if (immediate === true && enabled) {
      preservedCallback();
    }
  }, [immediate, preservedCallback, enabled]);
  useEffect10(() => {
    if (!enabled) {
      return;
    }
    const id = window.setInterval(preservedCallback, delay);
    return () => window.clearInterval(id);
  }, [delay, preservedCallback, enabled]);
}

// src/hooks/useIsomorphicLayoutEffect/useIsomorphicLayoutEffect.ts
import { useEffect as useEffect11, useLayoutEffect } from "react";
var isServer = typeof window === "undefined";
var useIsomorphicLayoutEffect = isServer ? useEffect11 : useLayoutEffect;

// src/hooks/useLoading/useLoading.ts
import { useCallback as useCallback12, useEffect as useEffect12, useMemo as useMemo4, useRef as useRef9, useState as useState6 } from "react";
function useLoading() {
  const [loading, setLoading] = useState6(false);
  const ref = useIsMountedRef();
  const startTransition = useCallback12(
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
  return useMemo4(() => [loading, startTransition], [loading, startTransition]);
}
function useIsMountedRef() {
  const ref = useRef9({ isMounted: true }).current;
  useEffect12(() => {
    ref.isMounted = true;
    return () => {
      ref.isMounted = false;
    };
  }, [ref]);
  return ref;
}

// src/hooks/useLongPress/useLongPress.ts
import { useCallback as useCallback13, useRef as useRef10 } from "react";
function useLongPress(onLongPress, { delay = 500, moveThreshold, onClick, onLongPressEnd } = {}) {
  const timeoutRef = useRef10(null);
  const isLongPressActiveRef = useRef10(false);
  const initialPositionRef = useRef10({ x: 0, y: 0 });
  const preservedOnLongPress = usePreservedCallback(onLongPress);
  const preservedOnClick = usePreservedCallback(onClick || (() => {
  }));
  const preservedOnLongPressEnd = usePreservedCallback(onLongPressEnd || (() => {
  }));
  const hasThreshold = moveThreshold?.x !== void 0 || moveThreshold?.y !== void 0;
  const getClientPosition = useCallback13((event) => {
    if ("touches" in event.nativeEvent) {
      const touch = event.nativeEvent.touches[0];
      return { x: touch.clientX, y: touch.clientY };
    }
    return {
      x: event.nativeEvent.clientX,
      y: event.nativeEvent.clientY
    };
  }, []);
  const isMovedBeyondThreshold = useCallback13(
    (event) => {
      const { x, y } = getClientPosition(event);
      const deltaX = Math.abs(x - initialPositionRef.current.x);
      const deltaY = Math.abs(y - initialPositionRef.current.y);
      return moveThreshold?.x !== void 0 && deltaX > moveThreshold.x || moveThreshold?.y !== void 0 && deltaY > moveThreshold.y;
    },
    [getClientPosition, moveThreshold]
  );
  const cancelLongPress = useCallback13(() => {
    if (timeoutRef.current !== null) {
      window.clearTimeout(timeoutRef.current);
      timeoutRef.current = null;
    }
  }, []);
  const handlePressStart = useCallback13(
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
  const handlePressEnd = useCallback13(
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
  const handlePressMove = useCallback13(
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
import { useCallback as useCallback14, useMemo as useMemo6, useState as useState7 } from "react";

// src/hooks/usePreservedReference/usePreservedReference.ts
import { useMemo as useMemo5, useRef as useRef11 } from "react";
function usePreservedReference(value, areValuesEqual = areDeeplyEqual) {
  const ref = useRef11(value);
  return useMemo5(() => {
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
  const [map, setMap] = useState7(() => new Map(initialState));
  const preservedInitialState = usePreservedReference(initialState);
  const set = useCallback14((key, value) => {
    setMap((prev) => {
      const nextMap = new Map(prev);
      nextMap.set(key, value);
      return nextMap;
    });
  }, []);
  const setAll = useCallback14((entries) => {
    setMap(() => new Map(entries));
  }, []);
  const remove = useCallback14((key) => {
    setMap((prev) => {
      const nextMap = new Map(prev);
      nextMap.delete(key);
      return nextMap;
    });
  }, []);
  const reset = useCallback14(() => {
    setMap(() => new Map(preservedInitialState));
  }, [preservedInitialState]);
  const actions = useMemo6(() => {
    return { set, setAll, remove, reset };
  }, [set, setAll, remove, reset]);
  return [map, actions];
}

// src/hooks/useOutsideClickEffect/useOutsideClickEffect.ts
import { useEffect as useEffect13, useRef as useRef12 } from "react";
function useOutsideClickEffect(container, callback) {
  const containers = useRef12([]);
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
  useEffect13(() => {
    containers.current = [container].flat(1).filter((item) => item != null);
  }, [container]);
  useEffect13(() => {
    document.addEventListener("click", handleDocumentClick);
    return () => {
      document.removeEventListener("click", handleDocumentClick);
    };
  }, [handleDocumentClick]);
}

// src/hooks/usePrevious/usePrevious.ts
import { useRef as useRef13 } from "react";
var strictEquals = (prev, next) => prev === next;
function usePrevious(state, compare = strictEquals) {
  const prevRef = useRef13(state);
  const currentRef = useRef13(state);
  const isFirstRender = useRef13(true);
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
import { useCallback as useCallback15, useRef as useRef14, useSyncExternalStore } from "react";

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
  const cache = useRef14({
    data: null,
    parsed: serializedDefaultValue
  });
  const getSnapshot = useCallback15(() => {
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
  const storageState = useSyncExternalStore(
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
  const setStorageState = useCallback15(
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
  const refreshStorageState = useCallback15(() => {
    setStorageState(getSnapshot());
  }, [storage, getSnapshot, setStorageState]);
  return ensureSerializable([storageState, setStorageState, refreshStorageState]);
}

// src/hooks/useThrottle/useThrottle.ts
import { useEffect as useEffect14, useMemo as useMemo7 } from "react";

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
  const throttledCallback = useMemo7(
    () => throttle(preservedCallback, wait, preservedOptions),
    [preservedOptions, preservedCallback, wait]
  );
  useEffect14(() => {
    return () => {
      throttledCallback.cancel();
    };
  }, [throttledCallback]);
  return throttledCallback;
}

// src/hooks/useTimeout/useTimeout.ts
import { useEffect as useEffect15 } from "react";
function useTimeout(callback, delay = 0) {
  const preservedCallback = usePreservedCallback(callback);
  useEffect15(() => {
    const timeoutId = window.setTimeout(preservedCallback, delay);
    return () => window.clearTimeout(timeoutId);
  }, [delay, preservedCallback]);
}

// src/hooks/useToggle/useToggle.ts
import { useReducer } from "react";
function useToggle(initialValue = false) {
  return useReducer(toggle, initialValue);
}
var toggle = (state) => !state;

// src/utils/buildContext/buildContext.tsx
import { createContext, useContext, useMemo as useMemo8 } from "react";
import { jsx as jsx3 } from "react/jsx-runtime";
function buildContext(contextName, defaultContextValues) {
  const Context = createContext(defaultContextValues ?? void 0);
  function Provider({ children, ...contextValues }) {
    const value = useMemo8(
      () => Object.keys(contextValues).length > 0 ? contextValues : null,
      // eslint-disable-next-line react-hooks/exhaustive-deps
      [...Object.values(contextValues)]
    );
    return /* @__PURE__ */ jsx3(Context.Provider, { value, children });
  }
  function useInnerContext() {
    const context = useContext(Context);
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
export {
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
};
