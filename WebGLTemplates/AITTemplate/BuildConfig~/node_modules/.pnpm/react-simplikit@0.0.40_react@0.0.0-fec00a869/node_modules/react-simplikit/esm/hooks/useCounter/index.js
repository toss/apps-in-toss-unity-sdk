// src/hooks/useCounter/useCounter.ts
import { useCallback, useState } from "react";
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
  const [count, setCountState] = useState(() => validateValue(initialValue));
  const validateValueMemoized = useCallback(validateValue, [min, max]);
  const setCount = useCallback(
    (value) => {
      setCountState((prev) => {
        const nextValue = typeof value === "function" ? value(prev) : value;
        return validateValueMemoized(nextValue);
      });
    },
    [validateValueMemoized]
  );
  const increment = useCallback(() => {
    setCount((prev) => prev + step);
  }, [setCount, step]);
  const decrement = useCallback(() => {
    setCount((prev) => prev - step);
  }, [setCount, step]);
  const reset = useCallback(() => {
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
export {
  useCounter
};
