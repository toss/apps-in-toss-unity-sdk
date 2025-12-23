// src/hooks/useMap/useMap.ts
import { useCallback, useMemo as useMemo2, useState } from "react";

// src/hooks/usePreservedReference/usePreservedReference.ts
import { useMemo, useRef } from "react";
function usePreservedReference(value, areValuesEqual = areDeeplyEqual) {
  const ref = useRef(value);
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

// src/hooks/useMap/useMap.ts
function useMap(initialState = /* @__PURE__ */ new Map()) {
  const [map, setMap] = useState(() => new Map(initialState));
  const preservedInitialState = usePreservedReference(initialState);
  const set = useCallback((key, value) => {
    setMap((prev) => {
      const nextMap = new Map(prev);
      nextMap.set(key, value);
      return nextMap;
    });
  }, []);
  const setAll = useCallback((entries) => {
    setMap(() => new Map(entries));
  }, []);
  const remove = useCallback((key) => {
    setMap((prev) => {
      const nextMap = new Map(prev);
      nextMap.delete(key);
      return nextMap;
    });
  }, []);
  const reset = useCallback(() => {
    setMap(() => new Map(preservedInitialState));
  }, [preservedInitialState]);
  const actions = useMemo2(() => {
    return { set, setAll, remove, reset };
  }, [set, setAll, remove, reset]);
  return [map, actions];
}
export {
  useMap
};
