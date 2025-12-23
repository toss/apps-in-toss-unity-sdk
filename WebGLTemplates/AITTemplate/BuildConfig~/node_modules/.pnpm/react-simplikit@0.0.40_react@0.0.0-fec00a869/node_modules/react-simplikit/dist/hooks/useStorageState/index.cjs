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

// src/hooks/useStorageState/index.ts
var useStorageState_exports = {};
__export(useStorageState_exports, {
  useStorageState: () => useStorageState
});
module.exports = __toCommonJS(useStorageState_exports);

// src/hooks/useStorageState/useStorageState.ts
var import_react = require("react");

// src/hooks/useStorageState/storage.ts
var MemoStorage = class {
  storage = /* @__PURE__ */ new Map();
  get(key) {
    return this.storage.get(key) ?? null;
  }
  set(key, value) {
    this.storage.set(key, value);
  }
  remove(key) {
    this.storage.delete(key);
  }
  clear() {
    this.storage.clear();
  }
};
var LocalStorage = class {
  static canUse() {
    const TEST_KEY = generateTestKey();
    try {
      localStorage.setItem(TEST_KEY, "test");
      localStorage.removeItem(TEST_KEY);
      return true;
    } catch {
      return false;
    }
  }
  get(key) {
    return localStorage.getItem(key);
  }
  set(key, value) {
    localStorage.setItem(key, value);
  }
  remove(key) {
    localStorage.removeItem(key);
  }
  clear() {
    localStorage.clear();
  }
};
var SessionStorage = class {
  static canUse() {
    const TEST_KEY = generateTestKey();
    try {
      sessionStorage.setItem(TEST_KEY, "test");
      sessionStorage.removeItem(TEST_KEY);
      return true;
    } catch {
      return false;
    }
  }
  get(key) {
    return sessionStorage.getItem(key);
  }
  set(key, value) {
    sessionStorage.setItem(key, value);
  }
  remove(key) {
    sessionStorage.removeItem(key);
  }
  clear() {
    sessionStorage.clear();
  }
};
function generateTestKey() {
  return new Array(4).fill(null).map(() => Math.random().toString(36).slice(2)).join("");
}
function generateStorage() {
  if (LocalStorage.canUse()) {
    return new LocalStorage();
  }
  return new MemoStorage();
}
function generateSessionStorage() {
  if (SessionStorage.canUse()) {
    return new SessionStorage();
  }
  return new MemoStorage();
}
var safeLocalStorage = generateStorage();
var safeSessionStorage = generateSessionStorage();

// src/hooks/useStorageState/useStorageState.ts
var listeners = /* @__PURE__ */ new Set();
var emitListeners = () => {
  listeners.forEach((listener) => listener());
};
function isPlainObject(value) {
  if (typeof value !== "object") {
    return false;
  }
  const proto = Object.getPrototypeOf(value);
  const hasObjectPrototype = proto === Object.prototype;
  if (!hasObjectPrototype) {
    return false;
  }
  return Object.prototype.toString.call(value) === "[object Object]";
}
var ensureSerializable = (value) => {
  if (value[0] != null && !["string", "number", "boolean"].includes(typeof value[0]) && !(isPlainObject(value[0]) || Array.isArray(value[0]))) {
    throw new Error("Received a non-serializable value");
  }
  return value;
};
function useStorageState(key, {
  storage = safeLocalStorage,
  defaultValue,
  ...options
} = {}) {
  const serializedDefaultValue = defaultValue;
  const cache = (0, import_react.useRef)({
    data: null,
    parsed: serializedDefaultValue
  });
  const getSnapshot = (0, import_react.useCallback)(() => {
    const deserializer = "deserializer" in options ? options.deserializer : JSON.parse;
    const data = storage.get(key);
    if (data !== cache.current.data) {
      try {
        cache.current.parsed = data != null ? deserializer(data) : defaultValue;
      } catch {
        cache.current.parsed = serializedDefaultValue;
      }
      cache.current.data = data;
    }
    return cache.current.parsed;
  }, [defaultValue, key, storage]);
  const storageState = (0, import_react.useSyncExternalStore)(
    (onStoreChange) => {
      listeners.add(onStoreChange);
      const handler = (event) => {
        if (event.key === key) {
          onStoreChange();
        }
      };
      window.addEventListener("storage", handler);
      return () => {
        listeners.delete(onStoreChange);
        window.removeEventListener("storage", handler);
      };
    },
    () => getSnapshot(),
    () => serializedDefaultValue
  );
  const setStorageState = (0, import_react.useCallback)(
    (value) => {
      const serializer = "serializer" in options ? options.serializer : JSON.stringify;
      const nextValue = typeof value === "function" ? value(getSnapshot()) : value;
      if (nextValue == null) {
        storage.remove(key);
      } else {
        storage.set(key, serializer(nextValue));
      }
      emitListeners();
    },
    [getSnapshot, key, storage]
  );
  const refreshStorageState = (0, import_react.useCallback)(() => {
    setStorageState(getSnapshot());
  }, [storage, getSnapshot, setStorageState]);
  return ensureSerializable([storageState, setStorageState, refreshStorageState]);
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  useStorageState
});
