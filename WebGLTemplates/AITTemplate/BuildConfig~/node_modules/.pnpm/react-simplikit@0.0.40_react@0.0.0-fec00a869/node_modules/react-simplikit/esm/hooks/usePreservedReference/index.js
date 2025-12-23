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
export {
  usePreservedReference
};
