"use strict";
var __defProp = Object.defineProperty;
var __getOwnPropDesc = Object.getOwnPropertyDescriptor;
var __getOwnPropNames = Object.getOwnPropertyNames;
var __hasOwnProp = Object.prototype.hasOwnProperty;
var __export = (target, all) => {
  for (var name in all)
    __defProp(target, name, { get: all[name], enumerable: true });
};
var __copyProps = (to, from, except, desc) => {
  if (from && typeof from === "object" || typeof from === "function") {
    for (let key of __getOwnPropNames(from))
      if (!__hasOwnProp.call(to, key) && key !== except)
        __defProp(to, key, { get: () => from[key], enumerable: !(desc = __getOwnPropDesc(from, key)) || desc.enumerable });
  }
  return to;
};
var __toCommonJS = (mod) => __copyProps(__defProp({}, "__esModule", { value: true }), mod);

// src/hooks/useLongPress/index.ts
var useLongPress_exports = {};
__export(useLongPress_exports, {
  useLongPress: () => useLongPress
});
module.exports = __toCommonJS(useLongPress_exports);

// src/hooks/useLongPress/useLongPress.ts
var import_react2 = require("react");

// src/hooks/usePreservedCallback/usePreservedCallback.ts
var import_react = require("react");
function usePreservedCallback(callback) {
  const callbackRef = (0, import_react.useRef)(callback);
  (0, import_react.useEffect)(() => {
    callbackRef.current = callback;
  }, [callback]);
  return (0, import_react.useCallback)((...args) => {
    return callbackRef.current(...args);
  }, []);
}

// src/hooks/useLongPress/useLongPress.ts
function useLongPress(onLongPress, { delay = 500, moveThreshold, onClick, onLongPressEnd } = {}) {
  const timeoutRef = (0, import_react2.useRef)(null);
  const isLongPressActiveRef = (0, import_react2.useRef)(false);
  const initialPositionRef = (0, import_react2.useRef)({ x: 0, y: 0 });
  const preservedOnLongPress = usePreservedCallback(onLongPress);
  const preservedOnClick = usePreservedCallback(onClick || (() => {
  }));
  const preservedOnLongPressEnd = usePreservedCallback(onLongPressEnd || (() => {
  }));
  const hasThreshold = moveThreshold?.x !== void 0 || moveThreshold?.y !== void 0;
  const getClientPosition = (0, import_react2.useCallback)((event) => {
    if ("touches" in event.nativeEvent) {
      const touch = event.nativeEvent.touches[0];
      return { x: touch.clientX, y: touch.clientY };
    }
    return {
      x: event.nativeEvent.clientX,
      y: event.nativeEvent.clientY
    };
  }, []);
  const isMovedBeyondThreshold = (0, import_react2.useCallback)(
    (event) => {
      const { x, y } = getClientPosition(event);
      const deltaX = Math.abs(x - initialPositionRef.current.x);
      const deltaY = Math.abs(y - initialPositionRef.current.y);
      return moveThreshold?.x !== void 0 && deltaX > moveThreshold.x || moveThreshold?.y !== void 0 && deltaY > moveThreshold.y;
    },
    [getClientPosition, moveThreshold]
  );
  const cancelLongPress = (0, import_react2.useCallback)(() => {
    if (timeoutRef.current !== null) {
      window.clearTimeout(timeoutRef.current);
      timeoutRef.current = null;
    }
  }, []);
  const handlePressStart = (0, import_react2.useCallback)(
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
  const handlePressEnd = (0, import_react2.useCallback)(
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
  const handlePressMove = (0, import_react2.useCallback)(
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
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  useLongPress
});
