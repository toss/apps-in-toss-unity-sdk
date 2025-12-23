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

// src/hooks/useIntersectionObserver/index.ts
var useIntersectionObserver_exports = {};
__export(useIntersectionObserver_exports, {
  useIntersectionObserver: () => useIntersectionObserver
});
module.exports = __toCommonJS(useIntersectionObserver_exports);

// src/hooks/useIntersectionObserver/useIntersectionObserver.ts
var import_react3 = require("react");

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

// src/hooks/useRefEffect/useRefEffect.ts
var import_react2 = require("react");
function useRefEffect(callback, deps) {
  const preservedCallback = usePreservedCallback(callback);
  const cleanupCallbackRef = (0, import_react2.useRef)(() => {
  });
  const effect = (0, import_react2.useCallback)(
    (element) => {
      cleanupCallbackRef.current();
      cleanupCallbackRef.current = () => {
      };
      if (element == null) {
        return;
      }
      const cleanup = preservedCallback(element);
      if (typeof cleanup === "function") {
        cleanupCallbackRef.current = cleanup;
      }
    },
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [preservedCallback, ...deps]
  );
  return effect;
}

// src/hooks/useIntersectionObserver/useIntersectionObserver.ts
function useIntersectionObserver(callback, options) {
  const preservedCallback = usePreservedCallback(callback);
  const observer = (0, import_react3.useMemo)(() => {
    if (typeof IntersectionObserver === "undefined") {
      return;
    }
    return new IntersectionObserver(([entry]) => {
      preservedCallback(entry);
    }, options);
  }, [preservedCallback, options]);
  return useRefEffect(
    (element) => {
      observer?.observe(element);
      return () => {
        observer?.unobserve(element);
      };
    },
    [preservedCallback, options]
  );
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  useIntersectionObserver
});
