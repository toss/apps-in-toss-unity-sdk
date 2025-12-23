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

// src/index.ts
var src_exports = {};
__export(src_exports, {
  Analytics: () => Analytics,
  withLoggingScreen: () => withLoggingScreen
});
module.exports = __toCommonJS(src_exports);

// src/components/LoggingArea.tsx
var import_react9 = require("react");
var import_react_native2 = require("react-native");

// src/components/LoggingImpression.tsx
var import_react_native = require("@granite-js/react-native");
var import_react7 = require("react");

// src/components/LoggingEvent.tsx
var import_react6 = require("react");

// src/hooks/useLogger.ts
var import_react5 = require("react");

// src/hooks/useLoggerContext.ts
var import_react3 = require("react");

// src/context/LoggingContext.ts
var import_react = require("react");
var LoggingContext = (0, import_react.createContext)(null);
function useLoggingContext() {
  const context = (0, import_react.useContext)(LoggingContext);
  if (context == null) {
    throw new Error("useLoggingContext should be used within <LoggingContext.Provider />");
  }
  return context;
}

// src/context/LoggingParamContext.ts
var import_react2 = require("react");
var LoggingParamsContext = (0, import_react2.createContext)(null);
function useLoggingParams() {
  return (0, import_react2.useContext)(LoggingParamsContext);
}

// src/hooks/useLoggerContext.ts
function useLoggerContext() {
  const context = useLoggingContext();
  const additionalParams = useLoggingParams();
  const { groupId, params: _params } = context ?? {};
  const params = (0, import_react3.useMemo)(() => ({ ..._params, ...additionalParams }), [_params, additionalParams]);
  return { groupId, params };
}

// src/context/LoggingStateContext.ts
var import_react4 = require("react");
var DEFAULT_STATE = {
  disabled: false
};
var LoggingStateContext = (0, import_react4.createContext)(DEFAULT_STATE);
function useLoggingState() {
  const state = (0, import_react4.useContext)(LoggingStateContext);
  if (state == null) {
    throw new Error("`useLoggingState` should be used within <LoggingStateContext.Provider />");
  }
  return state;
}

// src/logger/index.ts
var _config;
function getLogger() {
  let loggerFunction;
  if (__DEV__) {
    if (_config?.debug) {
      loggerFunction = devLogger;
    }
  } else {
    loggerFunction = _config?.logger;
  }
  return {
    screen: (groupId, params = {}) => {
      loggerFunction?.({
        log_type: "screen",
        log_name: createLogName(groupId, "screen"),
        params
      });
    },
    event: (groupId, params) => {
      const { log_name, ...otherParams } = params;
      loggerFunction?.({
        log_type: "event",
        log_name: log_name ?? createLogName(groupId, params.event_type),
        params: otherParams
      });
    }
  };
}
function setConfig(config) {
  _config = config;
}
function createLogName(groupId, logType) {
  return `${groupId}::${logType}`;
}
var devLogger = (params) => {
  console.debug("[Analytics]", JSON.stringify(params, null, 2));
};

// src/hooks/useLogger.ts
function useLogger() {
  const { groupId, params } = useLoggerContext();
  const { disabled } = useLoggingState();
  const logger = (0, import_react5.useMemo)(() => {
    if (groupId == null) {
      console.debug("Logging Context was not initialized.");
      return LOGGER_SHIM;
    }
    if (disabled) {
      console.debug("Logging is disabled.");
      return LOGGER_SHIM;
    }
    return {
      screen: (_groupId, params2) => {
        getLogger().screen(_groupId, params2);
      },
      event: (_params) => {
        getLogger().event(groupId, { ...params, ..._params });
      }
    };
  }, [groupId, params, disabled]);
  return logger;
}
var LOGGER_SHIM = {
  screen: noop,
  event: noop
};
function noop() {
}

// src/components/LoggingEvent.tsx
function LoggingEvent({
  children,
  capture,
  enabled = true,
  defer = false,
  params
}) {
  if (import_react6.Children.count(children) !== 1) {
    throw new Error("LoggingEvent must have exactly one child");
  }
  const child = import_react6.Children.only(children);
  if (!(0, import_react6.isValidElement)(child)) {
    throw new Error("LoggingEvent must have a valid child");
  }
  const logger = useLogger();
  const [logParams, push] = (0, import_react6.useReducer)(
    (logParams2, params2) => params2 != null ? [...logParams2, params2] : [],
    []
  );
  (0, import_react6.useEffect)(() => {
    if (logParams.length === 0) {
      return;
    }
    logParams.forEach((params2) => logger.event(params2));
    push(null);
  }, [logParams, logger]);
  return (0, import_react6.cloneElement)(child, {
    [capture]: async (...args) => {
      if (enabled) {
        if (defer) {
          push(typeof params === "function" ? params() : params);
        } else {
          logger.event(typeof params === "function" ? params() : params);
        }
      }
      const originalHandler = typeof child.props[capture] === "function" ? child.props[capture] : null;
      return originalHandler?.(...args);
    }
  });
}

// src/utils/addContext.ts
function addContext(params, context) {
  if (typeof params === "function") {
    return () => ({ ...params(), ...context });
  }
  return { ...params, ...context };
}

// src/components/LoggingImpression.tsx
var import_jsx_runtime = require("react/jsx-runtime");
var ImpressionFallbackContext = (0, import_react7.createContext)(false);
function LoggingImpression({
  enabled = true,
  impression: impressionType = "always",
  ...props
}) {
  const { params, ...rest } = props;
  const ImpressionAreaComponent = impressionType === "on-mount" ? import_react_native.ImpressionAreaOnMount : import_react_native.ImpressionArea;
  const isInFallbackProvider = (0, import_react7.useContext)(ImpressionFallbackContext);
  return /* @__PURE__ */ (0, import_jsx_runtime.jsx)(
    LoggingEvent,
    {
      capture: "onImpressionStart",
      enabled,
      params: addContext(params, { event_type: "impression" }),
      defer: true,
      children: /* @__PURE__ */ (0, import_jsx_runtime.jsx)(
        ImpressionAreaComponent,
        {
          UNSAFE__impressFallbackOnMount: impressionType === "with-fallback" || isInFallbackProvider,
          ...rest
        }
      )
    }
  );
}

// src/hooks/usePreservedReference.ts
var import_react8 = require("react");
function usePreservedReference(value, areValuesEqual = areDeeplyEqual) {
  const [reference, setReference] = (0, import_react8.useState)(value);
  (0, import_react8.useEffect)(() => {
    if (!areValuesEqual(value, reference)) {
      setReference(value);
    }
  }, [areValuesEqual, reference, value]);
  return reference;
}
function areDeeplyEqual(x, y) {
  return JSON.stringify(x) === JSON.stringify(y);
}

// src/internal/collectTextByFiber.ts
function collectTextByFiber(props, fiber) {
  for (const prop of props) {
    const accessibilityLabel = getFiberPropAsString(fiber, prop);
    if (accessibilityLabel != null) {
      return accessibilityLabel;
    }
  }
  return collectSiblings(fiber, collectTextByFiber.bind(null, props));
}
function collectSiblings(fiber, collector) {
  let targetText = "";
  for (let child = fiber.child; child != null; child = child?.sibling ?? null) {
    targetText = (targetText + " " + collector(child)).trim();
  }
  return targetText;
}
function getFiberPropAsString(fiber, property) {
  if (fiber.memoizedProps && typeof fiber.memoizedProps[property] === "string") {
    return fiber.memoizedProps[property];
  } else {
    return null;
  }
}

// src/internal/getReactInternalFiber.ts
function getReactInternalFiber(component) {
  return component._reactInternalFiber;
}

// src/components/LoggingArea.tsx
var import_jsx_runtime2 = require("react/jsx-runtime");
function LoggingArea({ children, params: _params = {}, ...props }) {
  const ref = (0, import_react9.useRef)(null);
  const logger = useLogger();
  const params = usePreservedReference(_params);
  return /* @__PURE__ */ (0, import_jsx_runtime2.jsx)(
    LoggingImpression,
    {
      params: () => ({
        text: INTERNAL__getTextContent(ref.current),
        ...params
      }),
      impression: "always",
      children: /* @__PURE__ */ (0, import_jsx_runtime2.jsx)(
        import_react_native2.Pressable,
        {
          ref,
          onPress: () => {
            logger.event({
              event_type: "click",
              text: INTERNAL__getTextContent(ref.current),
              ...params
            });
          },
          ...props,
          children: /* @__PURE__ */ (0, import_jsx_runtime2.jsx)(LoggingStateContext.Provider, { value: DISABLE_LOGGING_STATE, children })
        }
      )
    }
  );
}
var DISABLE_LOGGING_STATE = { disabled: true };
function INTERNAL__getTextContent(componentRef) {
  const fiber = getReactInternalFiber(componentRef);
  if (fiber == null) {
    return "";
  }
  return collectTextByFiber(["children"], fiber);
}

// src/components/LoggingPress.tsx
var import_react10 = require("react");
var import_jsx_runtime3 = require("react/jsx-runtime");
var LoggingPress = (0, import_react10.forwardRef)(({ children, enabled = true, params }, ref) => {
  return /* @__PURE__ */ (0, import_jsx_runtime3.jsx)(LoggingEvent, { capture: "onPress", enabled, params: addContext(params, { event_type: "click" }), children: (0, import_react10.cloneElement)(children, { ref }) });
});

// src/components/LoggingScreen.tsx
var import_react13 = require("react");

// src/hooks/useDefaultParams.ts
var import_react_native3 = require("@granite-js/react-native");

// src/utils/extractTimestampFromUUIDv7.ts
function extractTimestampFromUUIDv7(uuid) {
  const [part1, part2] = uuid.split("-");
  const hex = `${part1}${part2}`;
  const timestamp = parseInt(hex, 16);
  return timestamp;
}

// src/utils/getDeploymentId.ts
function safetyGetDeploymentId() {
  const appsInTossGlobal = global.__appsInToss;
  return typeof appsInTossGlobal?.deploymentId === "string" ? appsInTossGlobal.deploymentId : null;
}

// src/hooks/useDefaultParams.ts
var deploymentId = safetyGetDeploymentId();
var deploymentTimestamp = extractTimestampFromUUIDv7(deploymentId ?? "");
var referrer = (() => {
  try {
    return new URL((0, import_react_native3.getSchemeUri)()).searchParams.get("referrer");
  } catch {
    return null;
  }
})();
function useDefaultParams(additionalParams) {
  const params = usePreservedReference({
    referrer: referrer ?? null,
    deployment_id: deploymentId,
    deployment_timestamp: Number.isNaN(deploymentTimestamp) ? null : deploymentTimestamp,
    ...additionalParams
  });
  return params;
}

// src/hooks/useRoutePath.ts
var import_react_native4 = require("@granite-js/react-native");
function useRoutePath() {
  const navigation = (0, import_react_native4.useNavigation)();
  const navigationState = navigation.getState();
  const route = navigationState.routes[navigationState.index];
  const path = route?.name ?? route?.path;
  const params = route?.params;
  const paramString = Object.entries(params ?? {}).map(([key, value]) => `${key}=${value}`).join("&") || void 0;
  return usePreservedReference({ path: path ? path.split(/[?#]/)[0] : void 0, search: paramString });
}

// src/hooks/useScreenLogger.ts
var import_react12 = require("react");

// src/hooks/useVisibilitySession.ts
var import_react_native5 = require("@granite-js/react-native");
var import_react11 = require("react");
function useVisibilitySession() {
  const isCurrentlyVisibleRef = (0, import_react11.useRef)(true);
  const [consumed, setConsumed] = (0, import_react11.useState)(false);
  (0, import_react_native5.useVisibilityChange)((state) => {
    const isVisible = state === "visible";
    const visibleStateNotChanged = isVisible === isCurrentlyVisibleRef.current;
    if (visibleStateNotChanged) {
      return;
    }
    if (isVisible) {
      setConsumed(false);
    }
    isCurrentlyVisibleRef.current = isVisible;
  });
  return (0, import_react11.useMemo)(
    () => ({
      consume: () => {
        setConsumed(true);
        return !consumed;
      }
    }),
    [consumed]
  );
}

// src/hooks/useScreenLogger.ts
function useScreenLogger() {
  const session = useVisibilitySession();
  const logger = useLogger();
  return (0, import_react12.useMemo)(
    () => ({
      log: (groupId, params) => {
        const canConsume = session.consume();
        if (canConsume) {
          logger.screen(groupId, params);
        }
      }
    }),
    [logger, session]
  );
}

// src/components/LoggingScreen.tsx
var import_jsx_runtime4 = require("react/jsx-runtime");
function LoggingScreen({ params: _params = {}, ...props }) {
  const { path: groupId, search } = useRoutePath();
  const params = useDefaultParams({ ..._params, ...search ? { search } : null });
  const value = (0, import_react13.useMemo)(() => ({ groupId, params }), [groupId, params]);
  return groupId != null ? /* @__PURE__ */ (0, import_jsx_runtime4.jsx)(LoggingContext.Provider, { value, children: /* @__PURE__ */ (0, import_jsx_runtime4.jsx)(LoggingScreen.onMount, { groupId, params, ...props }) }) : /* @__PURE__ */ (0, import_jsx_runtime4.jsx)(import_jsx_runtime4.Fragment, { children: props.children });
}
LoggingScreen.onMount = function LoggingScreenOnMount({
  children,
  groupId,
  params,
  log = true
}) {
  const logger = useScreenLogger();
  (0, import_react13.useEffect)(() => {
    if (log) {
      logger.log(groupId, params);
    }
  }, [groupId, params, log, logger]);
  return /* @__PURE__ */ (0, import_jsx_runtime4.jsx)(import_jsx_runtime4.Fragment, { children });
};

// src/Analytics.ts
function init(options) {
  setConfig(options);
}
var Analytics = {
  // Functions
  init,
  // Components (Internal)
  Screen: LoggingScreen,
  // Components (Public)
  Press: LoggingPress,
  Impression: LoggingImpression,
  Area: LoggingArea
};

// src/hoc/withLoggingScreen.tsx
var import_react_native6 = require("@granite-js/react-native");
var import_react14 = require("react");

// src/internal/symbols.ts
var REACT_FORWARD_REF_TYPE = Symbol.for("react.forward_ref");
var REACT_MEMO_TYPE = Symbol.for("react.memo");

// src/utils/hoistNonReactStatics.ts
var REACT_STATICS = {
  childContextTypes: true,
  contextType: true,
  contextTypes: true,
  defaultProps: true,
  displayName: true,
  getDefaultProps: true,
  getDerivedStateFromError: true,
  getDerivedStateFromProps: true,
  mixins: true,
  propTypes: true,
  type: true
};
var KNOWN_STATICS = {
  name: true,
  length: true,
  prototype: true,
  caller: true,
  callee: true,
  arguments: true,
  arity: true
};
var FORWARD_REF_STATICS = {
  $$typeof: true,
  render: true,
  defaultProps: true,
  displayName: true,
  propTypes: true
};
var MEMO_STATICS = {
  $$typeof: true,
  compare: true,
  defaultProps: true,
  displayName: true,
  propTypes: true,
  type: true
};
var TYPE_STATICS = {};
TYPE_STATICS[REACT_FORWARD_REF_TYPE] = FORWARD_REF_STATICS;
TYPE_STATICS[REACT_MEMO_TYPE] = MEMO_STATICS;
function typeOf(component) {
  if (typeof component !== "object") {
    return component.$$typeof;
  }
}
function isMemo(component) {
  return typeOf(component) === REACT_MEMO_TYPE;
}
function getStatics(component) {
  if (isMemo(component)) {
    return MEMO_STATICS;
  }
  return TYPE_STATICS[component["$$typeof"]] || REACT_STATICS;
}
function hoistNonReactStatics(targetComponent, sourceComponent, blacklist) {
  if (typeof sourceComponent === "string") {
    return targetComponent;
  }
  const objectPrototype = Object.prototype;
  const inheritedComponent = Object.getPrototypeOf(sourceComponent);
  if (inheritedComponent && inheritedComponent !== objectPrototype) {
    hoistNonReactStatics(targetComponent, inheritedComponent, blacklist);
  }
  let keys = Object.getOwnPropertyNames(sourceComponent);
  if (Object.getOwnPropertySymbols) {
    keys = keys.concat(Object.getOwnPropertySymbols(sourceComponent));
  }
  const targetStatics = getStatics(targetComponent);
  const sourceStatics = getStatics(sourceComponent);
  for (let i = 0; i < keys.length; ++i) {
    const key = keys[i];
    if (key && !KNOWN_STATICS[key] && !(blacklist && blacklist[key]) && !(sourceStatics && sourceStatics[key]) && !(targetStatics && targetStatics[key])) {
      try {
        const descriptor = Object.getOwnPropertyDescriptor(sourceComponent, key);
        if (descriptor) {
          Object.defineProperty(targetComponent, key, descriptor);
        }
      } catch {
      }
    }
  }
  return targetComponent;
}

// src/hoc/withLoggingScreen.tsx
var import_jsx_runtime5 = require("react/jsx-runtime");
function withLoggingScreen(Component, options) {
  function WithLoggingScreen({
    loggerParams,
    children,
    hasIOScrollView = true,
    ...props
  }) {
    const hasIntersectionObserver = (0, import_react14.isValidElement)(children) && children.type === import_react_native6.IOScrollView;
    return /* @__PURE__ */ (0, import_jsx_runtime5.jsx)(LoggingScreen, { params: loggerParams, children: options?.augmentIntersectionObserver && !hasIntersectionObserver && hasIOScrollView ? /* @__PURE__ */ (0, import_jsx_runtime5.jsx)(Component, { ...props, children: /* @__PURE__ */ (0, import_jsx_runtime5.jsx)(import_react_native6.IOScrollView, { scrollEnabled: false, children }) }) : /* @__PURE__ */ (0, import_jsx_runtime5.jsx)(Component, { ...props, children }) });
  }
  return hoistNonReactStatics(WithLoggingScreen, Component);
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  Analytics,
  withLoggingScreen
});
