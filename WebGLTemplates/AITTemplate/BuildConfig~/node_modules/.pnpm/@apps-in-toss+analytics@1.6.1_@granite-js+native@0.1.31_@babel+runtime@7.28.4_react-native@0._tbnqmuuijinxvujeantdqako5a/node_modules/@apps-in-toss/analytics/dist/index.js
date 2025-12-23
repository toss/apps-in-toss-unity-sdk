// src/components/LoggingArea.tsx
import { useRef } from "react";
import { Pressable } from "react-native";

// src/components/LoggingImpression.tsx
import { ImpressionAreaOnMount, ImpressionArea } from "@granite-js/react-native";
import { createContext as createContext4, useContext as useContext4 } from "react";

// src/components/LoggingEvent.tsx
import { cloneElement, useEffect, useReducer, Children, isValidElement } from "react";

// src/hooks/useLogger.ts
import { useMemo as useMemo2 } from "react";

// src/hooks/useLoggerContext.ts
import { useMemo } from "react";

// src/context/LoggingContext.ts
import { createContext, useContext } from "react";
var LoggingContext = createContext(null);
function useLoggingContext() {
  const context = useContext(LoggingContext);
  if (context == null) {
    throw new Error("useLoggingContext should be used within <LoggingContext.Provider />");
  }
  return context;
}

// src/context/LoggingParamContext.ts
import { createContext as createContext2, useContext as useContext2 } from "react";
var LoggingParamsContext = createContext2(null);
function useLoggingParams() {
  return useContext2(LoggingParamsContext);
}

// src/hooks/useLoggerContext.ts
function useLoggerContext() {
  const context = useLoggingContext();
  const additionalParams = useLoggingParams();
  const { groupId, params: _params } = context ?? {};
  const params = useMemo(() => ({ ..._params, ...additionalParams }), [_params, additionalParams]);
  return { groupId, params };
}

// src/context/LoggingStateContext.ts
import { createContext as createContext3, useContext as useContext3 } from "react";
var DEFAULT_STATE = {
  disabled: false
};
var LoggingStateContext = createContext3(DEFAULT_STATE);
function useLoggingState() {
  const state = useContext3(LoggingStateContext);
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
  const logger = useMemo2(() => {
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
  if (Children.count(children) !== 1) {
    throw new Error("LoggingEvent must have exactly one child");
  }
  const child = Children.only(children);
  if (!isValidElement(child)) {
    throw new Error("LoggingEvent must have a valid child");
  }
  const logger = useLogger();
  const [logParams, push] = useReducer(
    (logParams2, params2) => params2 != null ? [...logParams2, params2] : [],
    []
  );
  useEffect(() => {
    if (logParams.length === 0) {
      return;
    }
    logParams.forEach((params2) => logger.event(params2));
    push(null);
  }, [logParams, logger]);
  return cloneElement(child, {
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
import { jsx } from "react/jsx-runtime";
var ImpressionFallbackContext = createContext4(false);
function LoggingImpression({
  enabled = true,
  impression: impressionType = "always",
  ...props
}) {
  const { params, ...rest } = props;
  const ImpressionAreaComponent = impressionType === "on-mount" ? ImpressionAreaOnMount : ImpressionArea;
  const isInFallbackProvider = useContext4(ImpressionFallbackContext);
  return /* @__PURE__ */ jsx(
    LoggingEvent,
    {
      capture: "onImpressionStart",
      enabled,
      params: addContext(params, { event_type: "impression" }),
      defer: true,
      children: /* @__PURE__ */ jsx(
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
import { useState, useEffect as useEffect2 } from "react";
function usePreservedReference(value, areValuesEqual = areDeeplyEqual) {
  const [reference, setReference] = useState(value);
  useEffect2(() => {
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
import { jsx as jsx2 } from "react/jsx-runtime";
function LoggingArea({ children, params: _params = {}, ...props }) {
  const ref = useRef(null);
  const logger = useLogger();
  const params = usePreservedReference(_params);
  return /* @__PURE__ */ jsx2(
    LoggingImpression,
    {
      params: () => ({
        text: INTERNAL__getTextContent(ref.current),
        ...params
      }),
      impression: "always",
      children: /* @__PURE__ */ jsx2(
        Pressable,
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
          children: /* @__PURE__ */ jsx2(LoggingStateContext.Provider, { value: DISABLE_LOGGING_STATE, children })
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
import { cloneElement as cloneElement2, forwardRef } from "react";
import { jsx as jsx3 } from "react/jsx-runtime";
var LoggingPress = forwardRef(({ children, enabled = true, params }, ref) => {
  return /* @__PURE__ */ jsx3(LoggingEvent, { capture: "onPress", enabled, params: addContext(params, { event_type: "click" }), children: cloneElement2(children, { ref }) });
});

// src/components/LoggingScreen.tsx
import { useMemo as useMemo5, useEffect as useEffect3 } from "react";

// src/hooks/useDefaultParams.ts
import { getSchemeUri } from "@granite-js/react-native";

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
    return new URL(getSchemeUri()).searchParams.get("referrer");
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
import { useNavigation } from "@granite-js/react-native";
function useRoutePath() {
  const navigation = useNavigation();
  const navigationState = navigation.getState();
  const route = navigationState.routes[navigationState.index];
  const path = route?.name ?? route?.path;
  const params = route?.params;
  const paramString = Object.entries(params ?? {}).map(([key, value]) => `${key}=${value}`).join("&") || void 0;
  return usePreservedReference({ path: path ? path.split(/[?#]/)[0] : void 0, search: paramString });
}

// src/hooks/useScreenLogger.ts
import { useMemo as useMemo4 } from "react";

// src/hooks/useVisibilitySession.ts
import { useVisibilityChange } from "@granite-js/react-native";
import { useState as useState2, useRef as useRef2, useMemo as useMemo3 } from "react";
function useVisibilitySession() {
  const isCurrentlyVisibleRef = useRef2(true);
  const [consumed, setConsumed] = useState2(false);
  useVisibilityChange((state) => {
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
  return useMemo3(
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
  return useMemo4(
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
import { Fragment, jsx as jsx4 } from "react/jsx-runtime";
function LoggingScreen({ params: _params = {}, ...props }) {
  const { path: groupId, search } = useRoutePath();
  const params = useDefaultParams({ ..._params, ...search ? { search } : null });
  const value = useMemo5(() => ({ groupId, params }), [groupId, params]);
  return groupId != null ? /* @__PURE__ */ jsx4(LoggingContext.Provider, { value, children: /* @__PURE__ */ jsx4(LoggingScreen.onMount, { groupId, params, ...props }) }) : /* @__PURE__ */ jsx4(Fragment, { children: props.children });
}
LoggingScreen.onMount = function LoggingScreenOnMount({
  children,
  groupId,
  params,
  log = true
}) {
  const logger = useScreenLogger();
  useEffect3(() => {
    if (log) {
      logger.log(groupId, params);
    }
  }, [groupId, params, log, logger]);
  return /* @__PURE__ */ jsx4(Fragment, { children });
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
import { IOScrollView } from "@granite-js/react-native";
import { isValidElement as isValidElement2 } from "react";

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
import { jsx as jsx5 } from "react/jsx-runtime";
function withLoggingScreen(Component, options) {
  function WithLoggingScreen({
    loggerParams,
    children,
    hasIOScrollView = true,
    ...props
  }) {
    const hasIntersectionObserver = isValidElement2(children) && children.type === IOScrollView;
    return /* @__PURE__ */ jsx5(LoggingScreen, { params: loggerParams, children: options?.augmentIntersectionObserver && !hasIntersectionObserver && hasIOScrollView ? /* @__PURE__ */ jsx5(Component, { ...props, children: /* @__PURE__ */ jsx5(IOScrollView, { scrollEnabled: false, children }) }) : /* @__PURE__ */ jsx5(Component, { ...props, children }) });
  }
  return hoistNonReactStatics(WithLoggingScreen, Component);
}
export {
  Analytics,
  withLoggingScreen
};
