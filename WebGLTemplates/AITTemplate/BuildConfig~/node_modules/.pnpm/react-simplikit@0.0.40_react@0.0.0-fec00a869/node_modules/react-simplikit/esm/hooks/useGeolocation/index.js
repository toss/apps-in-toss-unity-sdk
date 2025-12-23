// src/hooks/useGeolocation/useGeolocation.ts
import { useCallback, useEffect, useRef, useState } from "react";
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
  const [state, setState] = useState({
    loading: !!options?.mountBehavior,
    error: null,
    data: null
  });
  const [isTracking, setIsTracking] = useState(false);
  const watchIdRef = useRef(null);
  const checkGeolocationSupport = useCallback(() => {
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
  const handleSuccess = useCallback((position) => {
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
  const handleError = useCallback((error) => {
    const { code, message } = error;
    setState((prev) => ({
      ...prev,
      loading: false,
      error: new CustomGeoLocationError({ code, message })
    }));
  }, []);
  const getGeolocationOptions = useCallback(
    () => ({
      enableHighAccuracy: options?.enableHighAccuracy,
      maximumAge: options?.maximumAge,
      timeout: options?.timeout
    }),
    [options?.enableHighAccuracy, options?.maximumAge, options?.timeout]
  );
  const getCurrentPosition = useCallback(() => {
    if (!checkGeolocationSupport()) {
      return;
    }
    setState((prev) => ({ ...prev, loading: true }));
    navigator.geolocation.getCurrentPosition(handleSuccess, handleError, getGeolocationOptions());
  }, [handleSuccess, handleError, getGeolocationOptions, checkGeolocationSupport]);
  const startTracking = useCallback(() => {
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
  const stopTracking = useCallback(() => {
    if (watchIdRef.current === null) {
      return;
    }
    navigator.geolocation.clearWatch(watchIdRef.current);
    watchIdRef.current = null;
    setIsTracking(false);
  }, []);
  useEffect(() => {
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
export {
  useGeolocation
};
