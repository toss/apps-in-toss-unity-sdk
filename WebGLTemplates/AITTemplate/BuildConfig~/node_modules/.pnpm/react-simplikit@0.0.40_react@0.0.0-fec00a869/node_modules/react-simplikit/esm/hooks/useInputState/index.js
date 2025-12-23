// src/hooks/useInputState/useInputState.ts
import { useCallback, useState } from "react";
function useInputState(initialValue = "", transformValue = echo) {
  const [value, setValue] = useState(initialValue);
  const handleValueChange = useCallback(
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
export {
  useInputState
};
