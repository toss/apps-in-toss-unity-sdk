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

// src/hooks/useLoading/index.ts
var useLoading_exports = {};
__export(useLoading_exports, {
  useLoading: () => useLoading
});
module.exports = __toCommonJS(useLoading_exports);

// src/hooks/useLoading/useLoading.ts
var import_react = require("react");
function useLoading() {
  const [loading, setLoading] = (0, import_react.useState)(false);
  const ref = useIsMountedRef();
  const startTransition = (0, import_react.useCallback)(
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
  return (0, import_react.useMemo)(() => [loading, startTransition], [loading, startTransition]);
}
function useIsMountedRef() {
  const ref = (0, import_react.useRef)({ isMounted: true }).current;
  (0, import_react.useEffect)(() => {
    ref.isMounted = true;
    return () => {
      ref.isMounted = false;
    };
  }, [ref]);
  return ref;
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  useLoading
});
