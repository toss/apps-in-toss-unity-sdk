// src/utils/buildContext/buildContext.tsx
import { createContext, useContext, useMemo } from "react";
import { jsx } from "react/jsx-runtime";
function buildContext(contextName, defaultContextValues) {
  const Context = createContext(defaultContextValues ?? void 0);
  function Provider({ children, ...contextValues }) {
    const value = useMemo(
      () => Object.keys(contextValues).length > 0 ? contextValues : null,
      // eslint-disable-next-line react-hooks/exhaustive-deps
      [...Object.values(contextValues)]
    );
    return /* @__PURE__ */ jsx(Context.Provider, { value, children });
  }
  function useInnerContext() {
    const context = useContext(Context);
    if (context != null) {
      return context;
    }
    if (defaultContextValues != null) {
      return defaultContextValues;
    }
    throw new Error(`\`${contextName}Context\` must be used within \`${contextName}Provider\``);
  }
  return [Provider, useInnerContext];
}
export {
  buildContext
};
