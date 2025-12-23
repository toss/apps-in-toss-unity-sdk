// src/hooks/useBooleanState/useBooleanState.ts
import { useCallback, useState } from "react";
function useBooleanState(defaultValue = false) {
  const [bool, setBool] = useState(defaultValue);
  const setTrue = useCallback(() => {
    setBool(true);
  }, []);
  const setFalse = useCallback(() => {
    setBool(false);
  }, []);
  const toggle = useCallback(() => {
    setBool((prevBool) => !prevBool);
  }, []);
  return [bool, setTrue, setFalse, toggle];
}
export {
  useBooleanState
};
