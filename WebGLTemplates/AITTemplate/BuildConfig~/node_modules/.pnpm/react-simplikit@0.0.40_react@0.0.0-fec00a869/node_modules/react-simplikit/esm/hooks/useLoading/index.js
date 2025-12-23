// src/hooks/useLoading/useLoading.ts
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
function useLoading() {
  const [loading, setLoading] = useState(false);
  const ref = useIsMountedRef();
  const startTransition = useCallback(
    async (promise) => {
      try {
        setLoading(true);
        const data = await promise;
        return data;
      } finally {
        if (ref.isMounted) {
          setLoading(false);
        }
      }
    },
    [ref.isMounted]
  );
  return useMemo(() => [loading, startTransition], [loading, startTransition]);
}
function useIsMountedRef() {
  const ref = useRef({ isMounted: true }).current;
  useEffect(() => {
    ref.isMounted = true;
    return () => {
      ref.isMounted = false;
    };
  }, [ref]);
  return ref;
}
export {
  useLoading
};
