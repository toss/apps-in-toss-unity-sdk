// src/hooks/useLongPress/useLongPress.ts
import { useCallback as useCallback2, useRef as useRef2 } from "react";

// src/hooks/usePreservedCallback/usePreservedCallback.ts
import { useCallback, useEffect, useRef } from "react";
function usePreservedCallback(callback) {
  const callbackRef = useRef(callback);
  useEffect(() => {
    callbackRef.current = callback;
  }, [callback]);
  return useCallback((...args) => {
    return callbackRef.current(...args);
  }, []);
}

// src/hooks/useLongPress/useLongPress.ts
function useLongPress(onLongPress, { delay = 500, moveThreshold, onClick, onLongPressEnd } = {}) {
  const timeoutRef = useRef2(null);
  const isLongPressActiveRef = useRef2(false);
  const initialPositionRef = useRef2({ x: 0, y: 0 });
  const preservedOnLongPress = usePreservedCallback(onLongPress);
  const preservedOnClick = usePreservedCallback(onClick || (() => {
  }));
  const preservedOnLongPressEnd = usePreservedCallback(onLongPressEnd || (() => {
  }));
  const hasThreshold = moveThreshold?.x !== void 0 || moveThreshold?.y !== void 0;
  const getClientPosition = useCallback2((event) => {
    if ("touches" in event.nativeEvent) {
      const touch = event.nativeEvent.touches[0];
      return { x: touch.clientX, y: touch.clientY };
    }
    return {
      x: event.nativeEvent.clientX,
      y: event.nativeEvent.clientY
    };
  }, []);
  const isMovedBeyondThreshold = useCallback2(
    (event) => {
      const { x, y } = getClientPosition(event);
      const deltaX = Math.abs(x - initialPositionRef.current.x);
      const deltaY = Math.abs(y - initialPositionRef.current.y);
      return moveThreshold?.x !== void 0 && deltaX > moveThreshold.x || moveThreshold?.y !== void 0 && deltaY > moveThreshold.y;
    },
    [getClientPosition, moveThreshold]
  );
  const cancelLongPress = useCallback2(() => {
    if (timeoutRef.current !== null) {
      window.clearTimeout(timeoutRef.current);
      timeoutRef.current = null;
    }
  }, []);
  const handlePressStart = useCallback2(
    (event) => {
      cancelLongPress();
      const position = getClientPosition(event);
      initialPositionRef.current = position;
      isLongPressActiveRef.current = false;
      timeoutRef.current = window.setTimeout(() => {
        isLongPressActiveRef.current = true;
        preservedOnLongPress(event);
      }, delay);
    },
    [cancelLongPress, delay, getClientPosition, preservedOnLongPress]
  );
  const handlePressEnd = useCallback2(
    (event) => {
      if (isLongPressActiveRef.current) {
        preservedOnLongPressEnd(event);
      } else if (timeoutRef.current !== null) {
        preservedOnClick(event);
      }
      cancelLongPress();
      isLongPressActiveRef.current = false;
    },
    [cancelLongPress, preservedOnClick, preservedOnLongPressEnd]
  );
  const handlePressMove = useCallback2(
    (event) => {
      if (timeoutRef.current !== null && isMovedBeyondThreshold(event)) {
        cancelLongPress();
      }
    },
    [cancelLongPress, isMovedBeyondThreshold]
  );
  return {
    onMouseDown: handlePressStart,
    onMouseUp: handlePressEnd,
    onMouseLeave: cancelLongPress,
    onTouchStart: handlePressStart,
    onTouchEnd: handlePressEnd,
    ...hasThreshold ? { onTouchMove: handlePressMove, onMouseMove: handlePressMove } : {}
  };
}
export {
  useLongPress
};
