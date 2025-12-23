// src/hooks/useAsyncEffect/useAsyncEffect.ts
import { useEffect } from "react";
function useAsyncEffect(effect, deps) {
  useEffect(() => {
    let cleanup;
    effect().then((result) => {
      cleanup = result;
    });
    return () => {
      cleanup?.();
    };
  }, deps);
}
export {
  useAsyncEffect
};
