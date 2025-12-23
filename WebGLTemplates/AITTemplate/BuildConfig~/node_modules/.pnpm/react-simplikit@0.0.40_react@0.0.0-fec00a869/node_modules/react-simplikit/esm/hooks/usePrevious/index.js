// src/hooks/usePrevious/usePrevious.ts
import { useRef } from "react";
var strictEquals = (prev, next) => prev === next;
function usePrevious(state, compare = strictEquals) {
  const prevRef = useRef(state);
  const currentRef = useRef(state);
  const isFirstRender = useRef(true);
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
export {
  usePrevious
};
