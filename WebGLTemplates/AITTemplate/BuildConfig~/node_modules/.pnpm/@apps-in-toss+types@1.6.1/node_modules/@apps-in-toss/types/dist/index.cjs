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
  Accuracy: () => Accuracy,
  FetchAlbumPhotosPermissionError: () => FetchAlbumPhotosPermissionError,
  FetchContactsPermissionError: () => FetchContactsPermissionError,
  GetClipboardTextPermissionError: () => GetClipboardTextPermissionError,
  GetCurrentLocationPermissionError: () => GetCurrentLocationPermissionError,
  OpenCameraPermissionError: () => OpenCameraPermissionError,
  SetClipboardTextPermissionError: () => SetClipboardTextPermissionError,
  StartUpdateLocationPermissionError: () => StartUpdateLocationPermissionError
});
module.exports = __toCommonJS(index_exports);

// src/permissions/PermissionError.ts
var PermissionError = class extends Error {
  constructor({ methodName, message }) {
    super();
    this.name = `${methodName} permission error`;
    this.message = message;
  }
};

// src/permissions/FetchAlbumPhotosPermissionError.ts
var FetchAlbumPhotosPermissionError = class extends PermissionError {
  constructor() {
    super({ methodName: "fetchAlbumPhotos", message: "\uC0AC\uC9C4\uCCA9 \uAD8C\uD55C\uC774 \uAC70\uBD80\uB418\uC5C8\uC5B4\uC694." });
  }
};

// src/permissions/FetchContactsPermissionError.ts
var FetchContactsPermissionError = class extends PermissionError {
  constructor() {
    super({ methodName: "fetchContacts", message: "\uC5F0\uB77D\uCC98 \uAD8C\uD55C\uC774 \uAC70\uBD80\uB418\uC5C8\uC5B4\uC694." });
  }
};

// src/permissions/OpenCameraPermissionError.ts
var OpenCameraPermissionError = class extends PermissionError {
  constructor() {
    super({ methodName: "openCamera", message: "\uCE74\uBA54\uB77C \uAD8C\uD55C\uC774 \uAC70\uBD80\uB418\uC5C8\uC5B4\uC694." });
  }
};

// src/permissions/getCurrentLocation.ts
var Accuracy = /* @__PURE__ */ ((Accuracy2) => {
  Accuracy2[Accuracy2["Lowest"] = 1] = "Lowest";
  Accuracy2[Accuracy2["Low"] = 2] = "Low";
  Accuracy2[Accuracy2["Balanced"] = 3] = "Balanced";
  Accuracy2[Accuracy2["High"] = 4] = "High";
  Accuracy2[Accuracy2["Highest"] = 5] = "Highest";
  Accuracy2[Accuracy2["BestForNavigation"] = 6] = "BestForNavigation";
  return Accuracy2;
})(Accuracy || {});

// src/permissions/GetCurrentLocationPermissionError.ts
var GetCurrentLocationPermissionError = class extends PermissionError {
  constructor() {
    super({ methodName: "getCurrentLocation", message: "\uC704\uCE58 \uAD8C\uD55C\uC774 \uAC70\uBD80\uB418\uC5C8\uC5B4\uC694." });
  }
};

// src/permissions/StartUpdateLocationPermissionError.ts
var StartUpdateLocationPermissionError = GetCurrentLocationPermissionError;

// src/permissions/GetClipboardTextPermissionError.ts
var GetClipboardTextPermissionError = class extends PermissionError {
  constructor() {
    super({ methodName: "getClipboardText", message: "\uD074\uB9BD\uBCF4\uB4DC \uC77D\uAE30 \uAD8C\uD55C\uC774 \uAC70\uBD80\uB418\uC5C8\uC5B4\uC694." });
  }
};

// src/permissions/SetClipboardTextPermissionError.ts
var SetClipboardTextPermissionError = class extends PermissionError {
  constructor() {
    super({ methodName: "setClipboardText", message: "\uD074\uB9BD\uBCF4\uB4DC \uC4F0\uAE30 \uAD8C\uD55C\uC774 \uAC70\uBD80\uB418\uC5C8\uC5B4\uC694." });
  }
};
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  Accuracy,
  FetchAlbumPhotosPermissionError,
  FetchContactsPermissionError,
  GetClipboardTextPermissionError,
  GetCurrentLocationPermissionError,
  OpenCameraPermissionError,
  SetClipboardTextPermissionError,
  StartUpdateLocationPermissionError
});
