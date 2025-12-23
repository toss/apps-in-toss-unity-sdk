// src/hooks/useConditionalEffect/useConditionalEffect.ts
import { useCallback, useEffect, useRef } from "react";
function useConditionalEffect(effect, deps, condition) {
  const prevDepsRef = useRef(void 0);
  const memoizedCondition = useCallback(condition, deps);
  if (deps.length === 0) {
    console.warn(
      "useConditionalEffect received an empty dependency array. This may indicate missing dependencies and could lead to unexpected behavior."
    );
  }
  const shouldRun = memoizedCondition(prevDepsRef.current, deps);
  useEffect(() => {
    if (shouldRun) {
      const cleanup = effect();
      prevDepsRef.current = deps;
      return cleanup;
    }
    prevDepsRef.current = deps;
  }, deps);
}
export {
  useConditionalEffect
};
