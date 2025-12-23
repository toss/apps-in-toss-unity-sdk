// src/hooks/useVisibilityEvent/useVisibilityEvent.ts
import { useCallback, useEffect } from "react";
function useVisibilityEvent(callback, options = {}) {
  const handleVisibilityChange = useCallback(() => {
    callback(document.visibilityState);
  }, [callback]);
  useEffect(() => {
    if (options?.immediate ?? false) {
      handleVisibilityChange();
    }
    document.addEventListener("visibilitychange", handleVisibilityChange);
    return () => {
      document.removeEventListener("visibilitychange", handleVisibilityChange);
    };
  }, [handleVisibilityChange, options?.immediate]);
}
export {
  useVisibilityEvent
};
