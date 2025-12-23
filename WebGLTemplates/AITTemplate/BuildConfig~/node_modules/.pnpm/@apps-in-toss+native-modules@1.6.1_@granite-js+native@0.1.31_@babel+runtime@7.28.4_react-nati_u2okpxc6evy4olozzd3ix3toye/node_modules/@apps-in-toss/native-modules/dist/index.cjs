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
var index_exports = {};
__export(index_exports, {
  AppsInTossModule: () => AppsInTossModule,
  BedrockCoreModule: () => BedrockCoreModule,
  BedrockModule: () => BedrockModule,
  GoogleAdMob: () => GoogleAdMob,
  IAP: () => IAP,
  INTERNAL__AppsInTossModule: () => AppsInTossModuleInstance,
  INTERNAL__appBridgeHandler: () => INTERNAL__appBridgeHandler,
  INTERNAL__module: () => INTERNAL__module,
  StartUpdateLocationPermissionError: () => import_types8.StartUpdateLocationPermissionError,
  Storage: () => Storage,
  TossPay: () => TossPay,
  appLogin: () => appLogin,
  appsInTossEvent: () => appsInTossEvent,
  appsInTossSignTossCert: () => appsInTossSignTossCert,
  closeView: () => closeView,
  contactsViral: () => contactsViral,
  eventLog: () => eventLog,
  fetchAlbumPhotos: () => fetchAlbumPhotos,
  fetchContacts: () => fetchContacts,
  generateHapticFeedback: () => generateHapticFeedback,
  getClipboardText: () => getClipboardText,
  getCurrentLocation: () => getCurrentLocation,
  getDeviceId: () => getDeviceId,
  getGameCenterGameProfile: () => getGameCenterGameProfile,
  getIsTossLoginIntegratedService: () => getIsTossLoginIntegratedService,
  getLocale: () => getLocale,
  getNetworkStatus: () => getNetworkStatus,
  getOperationalEnvironment: () => getOperationalEnvironment,
  getPlatformOS: () => getPlatformOS,
  getSchemeUri: () => getSchemeUri2,
  getTossAppVersion: () => getTossAppVersion,
  getTossShareLink: () => getTossShareLink,
  getUserKeyForGame: () => getUserKeyForGame,
  grantPromotionRewardForGame: () => grantPromotionRewardForGame,
  iapCreateOneTimePurchaseOrder: () => iapCreateOneTimePurchaseOrder,
  isMinVersionSupported: () => isMinVersionSupported,
  onVisibilityChangedByTransparentServiceWeb: () => onVisibilityChangedByTransparentServiceWeb,
  openCamera: () => openCamera,
  openGameCenterLeaderboard: () => openGameCenterLeaderboard,
  openURL: () => openURL2,
  processProductGrant: () => processProductGrant,
  requestOneTimePurchase: () => requestOneTimePurchase,
  saveBase64Data: () => saveBase64Data,
  setClipboardText: () => setClipboardText,
  setDeviceOrientation: () => setDeviceOrientation,
  setIosSwipeGestureEnabled: () => setIosSwipeGestureEnabled,
  setScreenAwakeMode: () => setScreenAwakeMode,
  setSecureScreen: () => setSecureScreen,
  share: () => share,
  startUpdateLocation: () => startUpdateLocation,
  submitGameCenterLeaderBoardScore: () => submitGameCenterLeaderBoardScore
});
module.exports = __toCommonJS(index_exports);

// src/AppsInTossModule/native-event-emitter/appsInTossEvent.ts
var import_react_native6 = require("@granite-js/react-native");

// src/AppsInTossModule/native-event-emitter/event-plugins/UpdateLocationEvent.ts
var import_types = require("@apps-in-toss/types");
var import_react_native3 = require("@granite-js/react-native");

// src/AppsInTossModule/native-modules/AppsInTossModule.ts
var import_react_native = require("react-native");
var Module = import_react_native.TurboModuleRegistry.getEnforcing("AppsInTossModule");
var AppsInTossModuleInstance = Module;
var AppsInTossModule = Module;

// src/AppsInTossModule/native-modules/permissions/openPermissionDialog.ts
function openPermissionDialog(permission) {
  return AppsInTossModule.openPermissionDialog(permission);
}

// src/AppsInTossModule/native-modules/getPermission.ts
function getPermission(permission) {
  return AppsInTossModule.getPermission(permission);
}

// src/AppsInTossModule/native-modules/permissions/requestPermission.ts
async function requestPermission(permission) {
  const permissionStatus = await getPermission(permission);
  switch (permissionStatus) {
    case "allowed":
    case "denied":
      return permissionStatus;
    default:
      return openPermissionDialog(permission);
  }
}

// src/AppsInTossModule/native-event-emitter/nativeEventEmitter.ts
var import_react_native2 = require("react-native");
var nativeEventEmitter = new import_react_native2.NativeEventEmitter(AppsInTossModuleInstance);

// src/AppsInTossModule/native-event-emitter/event-plugins/UpdateLocationEvent.ts
var UpdateLocationEvent = class extends import_react_native3.GraniteEventDefinition {
  name = "updateLocationEvent";
  subscriptionCount = 0;
  ref = {
    remove: () => {
    }
  };
  remove() {
    if (--this.subscriptionCount === 0) {
      AppsInTossModuleInstance.stopUpdateLocation({});
    }
    this.ref.remove();
  }
  listener(options, onEvent, onError) {
    requestPermission({ name: "geolocation", access: "access" }).then((permissionStatus) => {
      if (permissionStatus === "denied") {
        onError(new import_types.GetCurrentLocationPermissionError());
        return;
      }
      void AppsInTossModuleInstance.startUpdateLocation(options).catch(onError);
      const subscription = nativeEventEmitter.addListener("updateLocation", onEvent);
      this.ref = {
        remove: () => subscription?.remove()
      };
      this.subscriptionCount++;
    }).catch(onError);
  }
};

// src/AppsInTossModule/native-event-emitter/internal/AppBridgeCallbackEvent.ts
var import_react_native4 = require("@granite-js/react-native");

// src/utils/generateUUID.ts
function generateUUID(placeholder) {
  return placeholder ? (placeholder ^ Math.random() * 16 >> placeholder / 4).toString(16) : (String(1e7) + 1e3 + 4e3 + 8e3 + 1e11).replace(/[018]/g, generateUUID);
}

// src/AppsInTossModule/native-event-emitter/internal/appBridge.ts
var INTERNAL__callbacks = /* @__PURE__ */ new Map();
function invokeAppBridgeCallback(id, ...args) {
  const callback = INTERNAL__callbacks.get(id);
  callback?.call(null, ...args);
  return Boolean(callback);
}
function invokeAppBridgeMethod(methodName, params, callbacks) {
  const { onSuccess, onError, ...appBridgeCallbacks } = callbacks;
  const { callbackMap, unregisterAll } = registerCallbacks(appBridgeCallbacks);
  const method = AppsInTossModuleInstance[methodName];
  if (method == null) {
    onError(new Error(`'${methodName}' is not defined in AppsInTossModule`));
    return unregisterAll;
  }
  const promise = method({
    params,
    callbacks: callbackMap
  });
  void promise.then(onSuccess).catch(onError);
  return unregisterAll;
}
function registerCallbacks(callbacks) {
  const callbackMap = {};
  for (const [callbackName, callback] of Object.entries(callbacks)) {
    const id = registerCallback(callback, callbackName);
    callbackMap[callbackName] = id;
  }
  const unregisterAll = () => {
    Object.values(callbackMap).forEach(unregisterCallback);
  };
  return { callbackMap, unregisterAll };
}
function registerCallback(callback, name = "unnamed") {
  const uniqueId = generateUUID();
  const callbackId = `${uniqueId}__${name}`;
  INTERNAL__callbacks.set(callbackId, callback);
  return callbackId;
}
function unregisterCallback(id) {
  INTERNAL__callbacks.delete(id);
}
function getCallbackIds() {
  return Array.from(INTERNAL__callbacks.keys());
}
var INTERNAL__appBridgeHandler = {
  invokeAppBridgeCallback,
  invokeAppBridgeMethod,
  registerCallback,
  unregisterCallback,
  getCallbackIds
};

// src/AppsInTossModule/native-event-emitter/internal/AppBridgeCallbackEvent.ts
var UNSAFE__nativeEventEmitter = nativeEventEmitter;
var AppBridgeCallbackEvent = class _AppBridgeCallbackEvent extends import_react_native4.GraniteEventDefinition {
  static INTERNAL__appBridgeSubscription;
  name = "appBridgeCallbackEvent";
  constructor() {
    super();
    this.registerAppBridgeCallbackEventListener();
  }
  remove() {
  }
  listener() {
  }
  registerAppBridgeCallbackEventListener() {
    if (_AppBridgeCallbackEvent.INTERNAL__appBridgeSubscription != null) {
      return;
    }
    _AppBridgeCallbackEvent.INTERNAL__appBridgeSubscription = UNSAFE__nativeEventEmitter.addListener(
      "appBridgeCallback",
      this.ensureInvokeAppBridgeCallback
    );
  }
  ensureInvokeAppBridgeCallback(result) {
    if (typeof result === "object" && typeof result.name === "string") {
      INTERNAL__appBridgeHandler.invokeAppBridgeCallback(result.name, result.params);
    } else {
      console.warn("Invalid app bridge callback result:", result);
    }
  }
};

// src/AppsInTossModule/native-event-emitter/internal/VisibilityChangedByTransparentServiceWebEvent.ts
var import_react_native5 = require("@granite-js/react-native");
var VisibilityChangedByTransparentServiceWebEvent = class extends import_react_native5.GraniteEventDefinition {
  name = "onVisibilityChangedByTransparentServiceWeb";
  subscription = null;
  remove() {
    this.subscription?.remove();
    this.subscription = null;
  }
  listener(options, onEvent, onError) {
    const subscription = nativeEventEmitter.addListener("visibilityChangedByTransparentServiceWeb", (params) => {
      if (this.isVisibilityChangedByTransparentServiceWebResult(params)) {
        if (params.callbackId === options.callbackId) {
          onEvent(params.isVisible);
        }
      } else {
        onError(new Error("Invalid visibility changed by transparent service web result"));
      }
    });
    this.subscription = subscription;
  }
  isVisibilityChangedByTransparentServiceWebResult(params) {
    return typeof params === "object" && typeof params.callbackId === "string" && typeof params.isVisible === "boolean";
  }
};

// src/AppsInTossModule/native-event-emitter/appsInTossEvent.ts
var appsInTossEvent = new import_react_native6.GraniteEvent([
  new UpdateLocationEvent(),
  // Internal events
  new AppBridgeCallbackEvent(),
  new VisibilityChangedByTransparentServiceWebEvent()
]);

// src/AppsInTossModule/native-modules/ads/googleAdMob.ts
var import_es_toolkit = require("es-toolkit");

// src/AppsInTossModule/native-modules/getOperationalEnvironment.ts
function getOperationalEnvironment() {
  return AppsInTossModule.operationalEnvironment;
}

// src/AppsInTossModule/native-modules/isMinVersionSupported.ts
var import_react_native7 = require("react-native");

// src/utils/compareVersion.ts
var SEMVER_REGEX = /^[v^~<>=]*?(\d+)(?:\.([x*]|\d+)(?:\.([x*]|\d+)(?:\.([x*]|\d+))?(?:-([\da-z\\-]+(?:\.[\da-z\\-]+)*))?(?:\+[\da-z\\-]+(?:\.[\da-z\\-]+)*)?)?)?$/i;
var isWildcard = (val) => ["*", "x", "X"].includes(val);
var tryParse = (val) => {
  const num = parseInt(val, 10);
  return isNaN(num) ? val : num;
};
var coerceTypes = (a, b) => {
  return typeof a === typeof b ? [a, b] : [String(a), String(b)];
};
var compareValues = (a, b) => {
  if (isWildcard(a) || isWildcard(b)) {
    return 0;
  }
  const [aVal, bVal] = coerceTypes(tryParse(a), tryParse(b));
  if (aVal > bVal) {
    return 1;
  }
  if (aVal < bVal) {
    return -1;
  }
  return 0;
};
var parseVersion = (version) => {
  if (typeof version !== "string") {
    throw new TypeError("Invalid argument: expected a string");
  }
  const match = version.match(SEMVER_REGEX);
  if (!match) {
    throw new Error(`Invalid semver: '${version}'`);
  }
  const [, major, minor, patch, build, preRelease] = match;
  return [major, minor, patch, build, preRelease];
};
var compareSegments = (a, b) => {
  const maxLength = Math.max(a.length, b.length);
  for (let i = 0; i < maxLength; i++) {
    const segA = a[i] ?? "0";
    const segB = b[i] ?? "0";
    const result = compareValues(segA, segB);
    if (result !== 0) {
      return result;
    }
  }
  return 0;
};
var compareVersions = (v1, v2) => {
  const seg1 = parseVersion(v1);
  const seg2 = parseVersion(v2);
  const preRelease1 = seg1.pop();
  const preRelease2 = seg2.pop();
  const mainCompare = compareSegments(seg1, seg2);
  if (mainCompare !== 0) {
    return mainCompare;
  }
  if (preRelease1 && preRelease2) {
    return compareSegments(preRelease1.split("."), preRelease2.split("."));
  }
  if (preRelease1) {
    return -1;
  }
  if (preRelease2) {
    return 1;
  }
  return 0;
};

// src/AppsInTossModule/native-modules/isMinVersionSupported.ts
function isMinVersionSupported(minVersions) {
  const operationalEnvironment = AppsInTossModule.operationalEnvironment;
  if (operationalEnvironment === "sandbox") {
    return true;
  }
  const currentVersion = AppsInTossModule.tossAppVersion;
  const isIOS = import_react_native7.Platform.OS === "ios";
  const minVersion = isIOS ? minVersions.ios : minVersions.android;
  if (minVersion === void 0) {
    return false;
  }
  if (minVersion === "always") {
    return true;
  }
  if (minVersion === "never") {
    return false;
  }
  return compareVersions(currentVersion, minVersion) >= 0;
}

// src/AppsInTossModule/native-modules/ads/googleAdMob.ts
function loadAdMobInterstitialAd(params) {
  if (!loadAdMobInterstitialAd.isSupported()) {
    params.onError(new Error(UNSUPPORTED_ERROR_MESSAGE));
    return import_es_toolkit.noop;
  }
  const { onEvent, onError, options } = params;
  const unregisterCallbacks = INTERNAL__appBridgeHandler.invokeAppBridgeMethod("loadAdMobInterstitialAd", options, {
    onAdClicked: () => {
      onEvent({ type: "clicked" });
    },
    onAdDismissed: () => {
      onEvent({ type: "dismissed" });
    },
    onAdFailedToShow: () => {
      onEvent({ type: "failedToShow" });
    },
    onAdImpression: () => {
      onEvent({ type: "impression" });
    },
    onAdShow: () => {
      onEvent({ type: "show" });
    },
    onSuccess: (result) => onEvent({ type: "loaded", data: result }),
    onError
  });
  return unregisterCallbacks;
}
function showAdMobInterstitialAd(params) {
  if (!showAdMobInterstitialAd.isSupported()) {
    params.onError(new Error(UNSUPPORTED_ERROR_MESSAGE));
    return import_es_toolkit.noop;
  }
  const { onEvent, onError, options } = params;
  const unregisterCallbacks = INTERNAL__appBridgeHandler.invokeAppBridgeMethod("showAdMobInterstitialAd", options, {
    onSuccess: () => onEvent({ type: "requested" }),
    onError
  });
  return unregisterCallbacks;
}
function loadAdMobRewardedAd(params) {
  if (!loadAdMobRewardedAd.isSupported()) {
    params.onError(new Error(UNSUPPORTED_ERROR_MESSAGE));
    return import_es_toolkit.noop;
  }
  const { onEvent, onError, options } = params;
  const unregisterCallbacks = INTERNAL__appBridgeHandler.invokeAppBridgeMethod("loadAdMobRewardedAd", options, {
    onAdClicked: () => {
      onEvent({ type: "clicked" });
    },
    onAdDismissed: () => {
      onEvent({ type: "dismissed" });
    },
    onAdFailedToShow: () => {
      onEvent({ type: "failedToShow" });
    },
    onAdImpression: () => {
      onEvent({ type: "impression" });
    },
    onAdShow: () => {
      onEvent({ type: "show" });
    },
    onUserEarnedReward: () => {
      onEvent({ type: "userEarnedReward" });
    },
    onSuccess: (result) => onEvent({ type: "loaded", data: result }),
    onError
  });
  return unregisterCallbacks;
}
function showAdMobRewardedAd(params) {
  if (!showAdMobRewardedAd.isSupported()) {
    params.onError(new Error(UNSUPPORTED_ERROR_MESSAGE));
    return import_es_toolkit.noop;
  }
  const { onEvent, onError, options } = params;
  const unregisterCallbacks = INTERNAL__appBridgeHandler.invokeAppBridgeMethod("showAdMobRewardedAd", options, {
    onSuccess: () => onEvent({ type: "requested" }),
    onError
  });
  return unregisterCallbacks;
}
var ANDROID_GOOGLE_AD_MOB_SUPPORTED_VERSION = "5.209.0";
var IOS_GOOGLE_AD_MOB_SUPPORTED_VERSION = "5.209.0";
var UNSUPPORTED_ERROR_MESSAGE = "This feature is not supported in the current environment";
var ENVIRONMENT = getOperationalEnvironment();
function createIsSupported() {
  return () => {
    if (ENVIRONMENT !== "toss") {
      return false;
    }
    return isMinVersionSupported({
      android: ANDROID_GOOGLE_AD_MOB_SUPPORTED_VERSION,
      ios: IOS_GOOGLE_AD_MOB_SUPPORTED_VERSION
    });
  };
}
loadAdMobInterstitialAd.isSupported = createIsSupported();
loadAdMobRewardedAd.isSupported = createIsSupported();
showAdMobInterstitialAd.isSupported = createIsSupported();
showAdMobRewardedAd.isSupported = createIsSupported();

// src/AppsInTossModule/native-modules/ads/googleAdMobV2.ts
var import_es_toolkit2 = require("es-toolkit");

// src/utils/getReferrer.ts
var import_react_native8 = require("@granite-js/react-native");
function getReferrer() {
  try {
    return new URL((0, import_react_native8.getSchemeUri)()).searchParams.get("referrer");
  } catch {
    return null;
  }
}

// src/AppsInTossModule/native-modules/ads/googleAdMobV2.ts
function loadAppsInTossAdMob(params) {
  if (!loadAppsInTossAdMob.isSupported()) {
    params.onError(new Error(UNSUPPORTED_ERROR_MESSAGE2));
    return import_es_toolkit2.noop;
  }
  const { onEvent, onError, options } = params;
  const referrer = getReferrer();
  const unregisterCallbacks = INTERNAL__appBridgeHandler.invokeAppBridgeMethod(
    "loadAppsInTossAdmob",
    { ...options, referrer },
    {
      onSuccess: (result) => onEvent({ type: "loaded", data: result }),
      onError
    }
  );
  return unregisterCallbacks;
}
function showAppsInTossAdMob(params) {
  if (!showAppsInTossAdMob.isSupported()) {
    params.onError(new Error(UNSUPPORTED_ERROR_MESSAGE2));
    return import_es_toolkit2.noop;
  }
  const { onEvent, onError, options } = params;
  const referrer = getReferrer();
  const unregisterCallbacks = INTERNAL__appBridgeHandler.invokeAppBridgeMethod(
    "showAppsInTossAdmob",
    { ...options, referrer },
    {
      onAdClicked: () => {
        onEvent({ type: "clicked" });
      },
      onAdDismissed: () => {
        onEvent({ type: "dismissed" });
      },
      onAdFailedToShow: () => {
        onEvent({ type: "failedToShow" });
      },
      onAdImpression: () => {
        onEvent({ type: "impression" });
      },
      onAdShow: () => {
        onEvent({ type: "show" });
      },
      onUserEarnedReward: (data) => {
        onEvent({ type: "userEarnedReward", data });
      },
      onSuccess: () => onEvent({ type: "requested" }),
      onError
    }
  );
  return unregisterCallbacks;
}
var ANDROID_GOOGLE_AD_MOB_SUPPORTED_VERSION2 = "5.227.0";
var IOS_GOOGLE_AD_MOB_SUPPORTED_VERSION2 = "5.227.0";
var UNSUPPORTED_ERROR_MESSAGE2 = "This feature is not supported in the current environment";
var ENVIRONMENT2 = getOperationalEnvironment();
function createIsSupported2() {
  return () => {
    if (ENVIRONMENT2 !== "toss") {
      return false;
    }
    return isMinVersionSupported({
      android: ANDROID_GOOGLE_AD_MOB_SUPPORTED_VERSION2,
      ios: IOS_GOOGLE_AD_MOB_SUPPORTED_VERSION2
    });
  };
}
loadAppsInTossAdMob.isSupported = createIsSupported2();
showAppsInTossAdMob.isSupported = createIsSupported2();

// src/AppsInTossModule/native-modules/checkoutPayment.ts
async function checkoutPayment(options) {
  return AppsInTossModule.checkoutPayment({ params: options });
}

// src/AppsInTossModule/native-modules/appLogin.ts
async function appLogin() {
  return AppsInTossModule.appLogin({});
}

// src/AppsInTossModule/native-modules/eventLog.ts
function normalizeParams(params) {
  return Object.fromEntries(
    Object.entries(params).filter(([, value]) => value !== void 0).map(([key, value]) => [key, String(value)])
  );
}
async function eventLog(params) {
  if (AppsInTossModule.operationalEnvironment === "sandbox") {
    console.log("[eventLogDebug]", {
      log_name: params.log_name,
      log_type: params.log_type,
      params: normalizeParams(params.params)
    });
    return;
  }
  const isSupported = isMinVersionSupported({
    android: "5.208.0",
    ios: "5.208.0"
  });
  if (!isSupported) {
    return;
  }
  return AppsInTossModule.eventLog({
    log_name: params.log_name,
    log_type: params.log_type,
    params: normalizeParams(params.params)
  });
}

// src/AppsInTossModule/native-modules/permissions/fetchAlbumPhotos/fetchAlbumPhotos.ts
var import_types2 = require("@apps-in-toss/types");

// src/AppsInTossModule/native-modules/permissions/createPermissionFunction.ts
function createPermissionFunction({
  handler,
  permission,
  error
}) {
  const permissionFunction = async (...args) => {
    const permissionStatus = await requestPermission(permission);
    if (permissionStatus === "denied") {
      throw new error();
    }
    return handler(...args);
  };
  permissionFunction.getPermission = () => getPermission(permission);
  permissionFunction.openPermissionDialog = () => openPermissionDialog(permission);
  return permissionFunction;
}

// src/AppsInTossModule/native-modules/permissions/fetchAlbumPhotos/fetchAlbumPhotos.ts
var DEFAULT_MAX_COUNT = 10;
var DEFAULT_MAX_WIDTH = 1024;
var fetchAlbumPhotos = createPermissionFunction({
  handler: async (options) => {
    return AppsInTossModule.fetchAlbumPhotos({
      ...options,
      maxCount: options?.maxCount ?? DEFAULT_MAX_COUNT,
      maxWidth: options?.maxWidth ?? DEFAULT_MAX_WIDTH
    });
  },
  permission: {
    name: "photos",
    access: "read"
  },
  error: import_types2.FetchAlbumPhotosPermissionError
});

// src/AppsInTossModule/native-modules/permissions/fetchContacts/fetchContacts.ts
var import_types3 = require("@apps-in-toss/types");
var fetchContacts = createPermissionFunction({
  handler: async (options) => {
    const contacts = await AppsInTossModule.fetchContacts(options);
    return {
      result: contacts.result,
      nextOffset: contacts.nextOffset ?? null,
      done: contacts.done
    };
  },
  permission: {
    name: "contacts",
    access: "read"
  },
  error: import_types3.FetchContactsPermissionError
});

// src/AppsInTossModule/native-modules/permissions/getClipboardText/getClipboardText.ts
var import_types4 = require("@apps-in-toss/types");
var getClipboardText = createPermissionFunction({
  handler: () => {
    return AppsInTossModule.getClipboardText({});
  },
  permission: {
    name: "clipboard",
    access: "read"
  },
  error: import_types4.GetClipboardTextPermissionError
});

// src/AppsInTossModule/native-modules/permissions/getCurrentLocation/getCurrentLocation.ts
var import_types5 = require("@apps-in-toss/types");
var getCurrentLocation = createPermissionFunction({
  handler: async (options) => {
    return AppsInTossModule.getCurrentLocation(options);
  },
  permission: {
    name: "geolocation",
    access: "access"
  },
  error: import_types5.GetCurrentLocationPermissionError
});

// src/AppsInTossModule/native-modules/permissions/setClipboardText/setClipboardText.ts
var import_types6 = require("@apps-in-toss/types");
var setClipboardText = createPermissionFunction({
  handler: (text) => {
    return AppsInTossModule.setClipboardText({ text });
  },
  permission: {
    name: "clipboard",
    access: "write"
  },
  error: import_types6.SetClipboardTextPermissionError
});

// src/AppsInTossModule/native-modules/permissions/openCamera/openCamera.ts
var import_types7 = require("@apps-in-toss/types");
var openCamera = createPermissionFunction({
  handler: (options) => {
    return AppsInTossModule.openCamera({
      base64: false,
      maxWidth: 1024,
      ...options
    });
  },
  permission: {
    name: "camera",
    access: "access"
  },
  error: import_types7.OpenCameraPermissionError
});

// src/AppsInTossModule/native-modules/getDeviceId.ts
function getDeviceId() {
  return AppsInTossModule.deviceId;
}

// src/AppsInTossModule/native-modules/getTossAppVersion.ts
function getTossAppVersion() {
  return AppsInTossModule.tossAppVersion;
}

// src/AppsInTossModule/native-modules/getTossShareLink.ts
var V2_MIN_VERSION = {
  android: "5.240.0",
  ios: "5.239.0"
};
async function getTossShareLink(path, ogImageUrl) {
  if (!isMinVersionSupported(V2_MIN_VERSION)) {
    return await getTossShareLinkV1(path);
  }
  const params = {
    params: {
      url: path,
      ogImageUrl
    }
  };
  const { shareLink } = await AppsInTossModule.getTossShareLink(params);
  return shareLink;
}
async function getTossShareLinkV1(path) {
  const { shareLink } = await AppsInTossModule.getTossShareLink({});
  const shareUrl = new URL(shareLink);
  shareUrl.searchParams.set("deep_link_value", path);
  shareUrl.searchParams.set("af_dp", path);
  return shareUrl.toString();
}

// src/AppsInTossModule/native-modules/iap.ts
var import_es_toolkit3 = require("es-toolkit");
function iapCreateOneTimePurchaseOrder(params) {
  const sku = params.sku ?? params.productId;
  return AppsInTossModule.iapCreateOneTimePurchaseOrder({ productId: sku });
}
function processProductGrant(params) {
  return AppsInTossModule.processProductGrant({ orderId: params.orderId, isProductGranted: params.isProductGranted });
}
function requestOneTimePurchase(params) {
  const { options, onEvent, onError } = params;
  const sku = options.sku ?? options.productId;
  const unregisterCallbacks = INTERNAL__appBridgeHandler.invokeAppBridgeMethod(
    "requestOneTimePurchase",
    { sku },
    {
      onPurchased: (params2) => {
        onEvent({ type: "purchased", data: params2 });
      },
      onSuccess: (result) => {
        onEvent({ type: "success", data: result });
      },
      onError: (error) => {
        onError(error);
      }
    }
  );
  return unregisterCallbacks;
}
function createOneTimePurchaseOrder(params) {
  const isIAPSupported = isMinVersionSupported({
    android: "5.219.0",
    ios: "5.219.0"
  });
  if (!isIAPSupported) {
    return import_es_toolkit3.noop;
  }
  const isProcessProductGrantSupported = isMinVersionSupported({
    android: "5.231.1",
    ios: "5.230.0"
  });
  const { options, onEvent, onError } = params;
  const sku = options.sku ?? options.productId;
  if (!isProcessProductGrantSupported) {
    const v1 = () => {
      AppsInTossModule.iapCreateOneTimePurchaseOrder({ productId: sku }).then((response) => {
        Promise.resolve(options.processProductGrant({ orderId: response.orderId })).then(() => {
          onEvent({ type: "success", data: response });
        }).catch((error) => {
          onError(error);
        });
      }).catch((error) => {
        onError(error);
      });
      return import_es_toolkit3.noop;
    };
    return v1();
  }
  const unregisterCallbacks = INTERNAL__appBridgeHandler.invokeAppBridgeMethod(
    "requestOneTimePurchase",
    { sku },
    {
      onPurchased: async (params2) => {
        const isProductGranted = await options.processProductGrant(params2);
        await AppsInTossModule.processProductGrant({ orderId: params2.orderId, isProductGranted });
      },
      onSuccess: (result) => {
        onEvent({ type: "success", data: result });
      },
      onError: (error) => {
        onError(error);
      }
    }
  );
  return unregisterCallbacks;
}
async function getProductItemList() {
  const isSupported = isMinVersionSupported({
    android: "5.219.0",
    ios: "5.219.0"
  });
  if (!isSupported) {
    return;
  }
  return AppsInTossModule.iapGetProductItemList({});
}
async function getPendingOrders() {
  const isSupported = isMinVersionSupported({
    android: "5.234.0",
    ios: "5.231.0"
  });
  if (!isSupported) {
    return;
  }
  return AppsInTossModule.getPendingOrders({});
}
async function getCompletedOrRefundedOrders(params) {
  const isSupported = isMinVersionSupported({
    android: "5.231.0",
    ios: "5.231.0"
  });
  if (!isSupported) {
    return;
  }
  return AppsInTossModule.getCompletedOrRefundedOrders(params ?? { key: null });
}
async function completeProductGrant(params) {
  const isSupported = isMinVersionSupported({
    android: "5.233.0",
    ios: "5.233.0"
  });
  if (!isSupported) {
    return;
  }
  return AppsInTossModule.completeProductGrant(params);
}
var IAP = {
  createOneTimePurchaseOrder,
  getProductItemList,
  getPendingOrders,
  getCompletedOrRefundedOrders,
  completeProductGrant
};

// src/AppsInTossModule/native-modules/saveBase64Data.ts
async function saveBase64Data(params) {
  const isSupported = isMinVersionSupported({
    android: "5.218.0",
    ios: "5.216.0"
  });
  if (!isSupported) {
    console.warn("saveBase64Data is not supported in this app version");
    return;
  }
  await AppsInTossModule.saveBase64Data(params);
}

// src/AppsInTossModule/native-modules/setDeviceOrientation.ts
async function setDeviceOrientation(options) {
  const isSupported = isMinVersionSupported({
    android: "5.215.0",
    ios: "5.215.0"
  });
  if (!isSupported) {
    return;
  }
  return AppsInTossModule.setDeviceOrientation(options);
}

// src/AppsInTossModule/native-modules/storage.ts
function getItem(key) {
  return AppsInTossModule.getStorageItem({ key });
}
function setItem(key, value) {
  return AppsInTossModule.setStorageItem({
    key,
    value
  });
}
function removeItem(key) {
  return AppsInTossModule.removeStorageItem({ key });
}
function clearItems() {
  return AppsInTossModule.clearStorage({});
}
var Storage = {
  getItem,
  setItem,
  removeItem,
  clearItems
};

// src/AppsInTossModule/native-modules/openGameCenterLeaderboard.ts
var import_react_native9 = require("@granite-js/react-native");

// src/AppsInTossModule/constants.ts
var GAME_CENTER_MIN_VERSION = {
  android: "5.221.0",
  ios: "5.221.0"
};
var GAME_USER_KEY_MIN_VERSION = {
  android: "5.232.0",
  ios: "5.232.0"
};
var GAME_PROMOTION_REWARD_MIN_VERSION = {
  android: "5.232.0",
  ios: "5.232.0"
};
var GET_IS_TOSS_LOGIN_INTEGRATED_SERVICE_MIN_VERSION = {
  android: "5.237.0",
  ios: "5.237.0"
};

// src/AppsInTossModule/native-modules/openGameCenterLeaderboard.ts
async function openGameCenterLeaderboard() {
  if (!isMinVersionSupported(GAME_CENTER_MIN_VERSION)) {
    return;
  }
  const appName = global.__granite?.app?.name;
  if (appName == null) {
    throw new Error("Cannot get app name");
  }
  const url = new URL("servicetoss://game-center/leaderboard?_navbar=hide");
  url.searchParams.set("appName", appName);
  url.searchParams.set("referrer", `appsintoss.${appName}`);
  return (0, import_react_native9.openURL)(url.toString());
}

// src/AppsInTossModule/native-modules/getGameCenterGameProfile.ts
async function getGameCenterGameProfile() {
  const isSupported = isMinVersionSupported(GAME_CENTER_MIN_VERSION);
  if (!isSupported) {
    return;
  }
  return AppsInTossModule.getGameCenterGameProfile({});
}

// src/AppsInTossModule/native-modules/submitGameCenterLeaderBoardScore.ts
async function submitGameCenterLeaderBoardScore(params) {
  const isSupported = isMinVersionSupported(GAME_CENTER_MIN_VERSION);
  if (!isSupported) {
    return;
  }
  return AppsInTossModule.submitGameCenterLeaderBoardScore(params);
}

// src/AppsInTossModule/native-modules/getUserKeyForGame.ts
async function getUserKeyForGame() {
  const isSupported = isMinVersionSupported(GAME_USER_KEY_MIN_VERSION);
  if (!isSupported) {
    return;
  }
  try {
    const response = await AppsInTossModule.getUserKeyForGame({});
    if (response.type === "HASH") {
      return response;
    }
    if (response.type === "NOT_AVAILABLE") {
      return "INVALID_CATEGORY";
    }
    return "ERROR";
  } catch (_) {
    return "ERROR";
  }
}

// src/AppsInTossModule/native-modules/grantPromotionRewardForGame.ts
function isGrantPromotionRewardForGameError(error) {
  return typeof error === "object" && error !== null && "code" in error && typeof error.code === "string" && "message" in error && typeof error.message === "string";
}
async function grantPromotionRewardForGame({
  params
}) {
  const isSupported = isMinVersionSupported(GAME_PROMOTION_REWARD_MIN_VERSION);
  if (!isSupported) {
    return;
  }
  try {
    const response = await AppsInTossModule.grantPromotionRewardForGame({ params });
    if (response.key) {
      return response;
    }
    return "ERROR";
  } catch (error) {
    if (isGrantPromotionRewardForGameError(error)) {
      return {
        errorCode: error.code,
        message: error.message
      };
    }
    return "ERROR";
  }
}

// src/AppsInTossModule/native-modules/getIsTossLoginIntegratedService.ts
async function getIsTossLoginIntegratedService() {
  const isSupported = isMinVersionSupported(GET_IS_TOSS_LOGIN_INTEGRATED_SERVICE_MIN_VERSION);
  if (!isSupported) {
    return;
  }
  const response = await AppsInTossModule.getIsTossLoginIntegratedService({});
  return response;
}

// src/AppsInTossModule/native-event-emitter/contactsViral.ts
function contactsViral(params) {
  const isSupported = isMinVersionSupported({
    android: "5.223.0",
    ios: "5.223.0"
  });
  if (!isSupported) {
    return () => {
    };
  }
  const { onEvent, onError, options } = params;
  const unregisterCallbacks = INTERNAL__appBridgeHandler.invokeAppBridgeMethod("contactsViral", options, {
    onRewardFromContactsViral: (result) => {
      onEvent({ type: "sendViral", data: result });
    },
    onSuccess: (result) => {
      onEvent({ type: "close", data: result });
    },
    onError
  });
  return unregisterCallbacks;
}

// src/AppsInTossModule/native-modules/appsInTossSignTossCert.ts
var MIN_VERSION_BY_USER_TYPE = {
  USER_PERSONAL: {
    android: "5.233.0",
    ios: "5.233.0"
  },
  USER_NONE: {
    android: "5.236.0",
    ios: "5.236.0"
  }
};
async function appsInTossSignTossCert(params) {
  const minVersion = params.skipConfirmDoc === true ? MIN_VERSION_BY_USER_TYPE.USER_NONE : MIN_VERSION_BY_USER_TYPE.USER_PERSONAL;
  const isSupported = isMinVersionSupported(minVersion);
  if (!isSupported) {
    console.warn("appsInTossSignTossCert is not supported in this app version");
    return;
  }
  await AppsInTossModule.appsInTossSignTossCert({ params });
}

// src/AppsInTossModule/native-modules/index.ts
var TossPay = {
  checkoutPayment
};
var GoogleAdMob = {
  loadAdMobInterstitialAd,
  showAdMobInterstitialAd,
  loadAdMobRewardedAd,
  showAdMobRewardedAd,
  loadAppsInTossAdMob,
  showAppsInTossAdMob
};

// src/AppsInTossModule/native-event-emitter/startUpdateLocation.ts
function startUpdateLocation(eventParams) {
  return appsInTossEvent.addEventListener("updateLocationEvent", eventParams);
}
startUpdateLocation.openPermissionDialog = getCurrentLocation.openPermissionDialog;
startUpdateLocation.getPermission = getCurrentLocation.getPermission;

// src/AppsInTossModule/native-event-emitter/StartUpdateLocationPermissionError.ts
var import_types8 = require("@apps-in-toss/types");

// src/AppsInTossModule/native-event-emitter/internal/onVisibilityChangedByTransparentServiceWeb.ts
function onVisibilityChangedByTransparentServiceWeb(eventParams) {
  return appsInTossEvent.addEventListener("onVisibilityChangedByTransparentServiceWeb", eventParams);
}

// src/BedrockModule/native-modules/natives/BedrockModule.ts
var import_react_native10 = require("react-native");
var BedrockModule = import_react_native10.NativeModules.BedrockModule;

// src/BedrockModule/native-modules/natives/closeView.ts
async function closeView() {
  return BedrockModule.closeView();
}

// src/BedrockModule/native-modules/natives/getLocale.ts
var import_react_native11 = require("react-native");
function getLocale() {
  const locale = BedrockModule?.DeviceInfo?.locale ?? "ko-KR";
  if (import_react_native11.Platform.OS === "android") {
    return replaceUnderbarToHypen(locale);
  }
  return locale;
}
function replaceUnderbarToHypen(locale) {
  return locale.replace(/_/g, "-");
}

// src/BedrockModule/native-modules/natives/getSchemeUri.ts
function getSchemeUri2() {
  return BedrockModule.schemeUri;
}

// src/BedrockModule/native-modules/natives/generateHapticFeedback/index.ts
function generateHapticFeedback(options) {
  return BedrockModule.generateHapticFeedback(options);
}

// src/BedrockModule/native-modules/natives/share.ts
async function share(message) {
  BedrockModule.share(message);
}

// src/BedrockModule/native-modules/natives/setSecureScreen.ts
function setSecureScreen(options) {
  return BedrockModule.setSecureScreen(options);
}

// src/BedrockModule/native-modules/natives/setScreenAwakeMode.ts
async function setScreenAwakeMode(options) {
  return BedrockModule.setScreenAwakeMode(options);
}

// src/BedrockModule/native-modules/natives/getNetworkStatus/index.ts
function getNetworkStatus() {
  return BedrockModule.getNetworkStatus();
}

// src/BedrockModule/native-modules/natives/setIosSwipeGestureEnabled.ts
async function setIosSwipeGestureEnabled(options) {
  if (BedrockModule.setIosSwipeGestureEnabled == null) {
    return;
  }
  return BedrockModule.setIosSwipeGestureEnabled(options);
}

// src/BedrockModule/native-modules/natives/openURL.ts
var import_react_native12 = require("react-native");
function openURL2(url) {
  return import_react_native12.Linking.openURL(url);
}

// src/BedrockModule/native-modules/natives/getPlatformOS.ts
var import_react_native13 = require("react-native");
function getPlatformOS() {
  return import_react_native13.Platform.OS;
}

// src/BedrockModule/native-modules/core/BedrockCoreModule.ts
var import_react_native14 = require("react-native");
var BedrockCoreModule = import_react_native14.NativeModules.BedrockCoreModule;

// src/AppsInTossModule/native-modules/tossCore.ts
var import_react_native15 = require("react-native");
var TossCoreModule = import_react_native15.NativeModules.TossCoreModule;
function tossCoreEventLog(params) {
  const supported = isMinVersionSupported({ ios: "5.210.0", android: "5.210.0" });
  const isSandbox = getOperationalEnvironment() === "sandbox";
  if (!supported || isSandbox) {
    return;
  }
  TossCoreModule.eventLog({
    params: {
      log_name: params.log_name,
      log_type: params.log_type,
      params: params.params
    }
  });
}

// src/index.ts
var INTERNAL__module = {
  tossCoreEventLog
};
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  AppsInTossModule,
  BedrockCoreModule,
  BedrockModule,
  GoogleAdMob,
  IAP,
  INTERNAL__AppsInTossModule,
  INTERNAL__appBridgeHandler,
  INTERNAL__module,
  StartUpdateLocationPermissionError,
  Storage,
  TossPay,
  appLogin,
  appsInTossEvent,
  appsInTossSignTossCert,
  closeView,
  contactsViral,
  eventLog,
  fetchAlbumPhotos,
  fetchContacts,
  generateHapticFeedback,
  getClipboardText,
  getCurrentLocation,
  getDeviceId,
  getGameCenterGameProfile,
  getIsTossLoginIntegratedService,
  getLocale,
  getNetworkStatus,
  getOperationalEnvironment,
  getPlatformOS,
  getSchemeUri,
  getTossAppVersion,
  getTossShareLink,
  getUserKeyForGame,
  grantPromotionRewardForGame,
  iapCreateOneTimePurchaseOrder,
  isMinVersionSupported,
  onVisibilityChangedByTransparentServiceWeb,
  openCamera,
  openGameCenterLeaderboard,
  openURL,
  processProductGrant,
  requestOneTimePurchase,
  saveBase64Data,
  setClipboardText,
  setDeviceOrientation,
  setIosSwipeGestureEnabled,
  setScreenAwakeMode,
  setSecureScreen,
  share,
  startUpdateLocation,
  submitGameCenterLeaderBoardScore
});
