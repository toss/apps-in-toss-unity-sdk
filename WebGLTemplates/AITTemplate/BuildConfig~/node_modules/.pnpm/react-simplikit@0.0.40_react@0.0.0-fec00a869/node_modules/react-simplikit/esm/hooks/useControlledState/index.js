// src/hooks/useControlledState/useControlledState.ts
import { useCallback, useState } from "react";
function useControlledState({
  value: valueProp,
  defaultValue,
  onChange,
  equalityFn = Object.is
}) {
  const [uncontrolledState, setUncontrolledState] = useState(defaultValue);
  const controlled = valueProp !== void 0;
  const value = controlled ? valueProp : uncontrolledState;
  const setValue = useCallback(
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
export {
  useControlledState
};
