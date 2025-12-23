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

// src/hooks/useGeolocation/index.ts
var useGeolocation_exports = {};
__export(useGeolocation_exports, {
  useGeolocation: () => useGeolocation
});
module.exports = __toCommonJS(useGeolocation_exports);

// src/hooks/useGeolocation/useGeolocation.ts
var import_react = require("react");
var CustomGeoLocationError = class extends Error {
  code;
  constructor({ code, message }) {
    super(message);
    this.name = "CustomGeoLocationError";
    this.code = code;
  }
};
var GeolocationMountBehavior = {
  GET: "get",
  WATCH: "watch"
};
function useGeolocation(options) {
  const [state, setState] = (0, import_react.useState)({
    loading: !!options?.mountBehavior,
    error: null,
    data: null
  });
  const [isTracking, setIsTracking] = (0, import_react.useState)(false);
  const watchIdRef = (0, import_react.useRef)(null);
  const checkGeolocationSupport = (0, import_react.useCallback)(() => {
    if (typeof window === "undefined" || navigator.geolocation === void 0) {
      setState((prev) => ({
        ...prev,
        loading: false,
        error: new CustomGeoLocationError({
          code: 0,
          message: "Geolocation is not supported by this environment."
        })
      }));
      return false;
    }
    return true;
  }, []);
  const handleSuccess = (0, import_react.useCallback)((position) => {
    const { coords } = position;
    setState((prev) => ({
      ...prev,
      loading: false,
      error: null,
      data: {
        latitude: coords.latitude,
        longitude: coords.longitude,
        accuracy: coords.accuracy,
        altitude: coords.altitude,
        altitudeAccuracy: coords.altitudeAccuracy,
        heading: coords.heading,
        speed: coords.speed,
        timestamp: position.timestamp
      }
    }));
  }, []);
  const handleError = (0, import_react.useCallback)((error) => {
    const { code, message } = error;
    setState((prev) => ({
      ...prev,
      loading: false,
      error: new CustomGeoLocationError({ code, message })
    }));
  }, []);
  const getGeolocationOptions = (0, import_react.useCallback)(
    () => ({
      enableHighAccuracy: options?.enableHighAccuracy,
      maximumAge: options?.maximumAge,
      timeout: options?.timeout
    }),
    [options?.enableHighAccuracy, options?.maximumAge, options?.timeout]
  );
  const getCurrentPosition = (0, import_react.useCallback)(() => {
    if (!checkGeolocationSupport()) {
      return;
    }
    setState((prev) => ({ ...prev, loading: true }));
    navigator.geolocation.getCurrentPosition(handleSuccess, handleError, getGeolocationOptions());
  }, [handleSuccess, handleError, getGeolocationOptions, checkGeolocationSupport]);
  const startTracking = (0, import_react.useCallback)(() => {
    if (!checkGeolocationSupport()) {
      return;
    }
    if (watchIdRef.current !== null) {
      navigator.geolocation.clearWatch(watchIdRef.current);
    }
    setState((prev) => ({ ...prev, loading: true }));
    watchIdRef.current = navigator.geolocation.watchPosition(
      (position) => {
        setIsTracking(true);
        handleSuccess(position);
      },
      handleError,
      getGeolocationOptions()
    );
  }, [handleSuccess, handleError, getGeolocationOptions, checkGeolocationSupport]);
  const stopTracking = (0, import_react.useCallback)(() => {
    if (watchIdRef.current === null) {
      return;
    }
    navigator.geolocation.clearWatch(watchIdRef.current);
    watchIdRef.current = null;
    setIsTracking(false);
  }, []);
  (0, import_react.useEffect)(() => {
    if (options?.mountBehavior === GeolocationMountBehavior.WATCH) {
      startTracking();
    } else if (options?.mountBehavior === GeolocationMountBehavior.GET) {
      getCurrentPosition();
    }
    return () => {
      if (watchIdRef.current !== null) {
        navigator.geolocation.clearWatch(watchIdRef.current);
        watchIdRef.current = null;
      }
    };
  }, [options?.mountBehavior, getCurrentPosition, startTracking]);
  return {
    ...state,
    getCurrentPosition,
    startTracking,
    stopTracking,
    isTracking
  };
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  useGeolocation
});
