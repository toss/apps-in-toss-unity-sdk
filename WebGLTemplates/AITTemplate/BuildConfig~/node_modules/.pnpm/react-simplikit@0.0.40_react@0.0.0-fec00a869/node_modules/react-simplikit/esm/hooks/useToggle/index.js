// src/hooks/useToggle/useToggle.ts
import { useReducer } from "react";
function useToggle(initialValue = false) {
  return useReducer(toggle, initialValue);
}
var toggle = (state) => !state;
export {
  useToggle
};
