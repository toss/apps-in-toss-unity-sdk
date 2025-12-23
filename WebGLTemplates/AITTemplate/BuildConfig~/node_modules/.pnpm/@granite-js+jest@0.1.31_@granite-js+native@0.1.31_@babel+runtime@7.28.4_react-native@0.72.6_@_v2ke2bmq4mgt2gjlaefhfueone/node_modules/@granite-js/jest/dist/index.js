"use strict";
var __create = Object.create;
var __defProp = Object.defineProperty;
var __getOwnPropDesc = Object.getOwnPropertyDescriptor;
var __getOwnPropNames = Object.getOwnPropertyNames;
var __getProtoOf = Object.getPrototypeOf;
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
var __toESM = (mod, isNodeMode, target) => (target = mod != null ? __create(__getProtoOf(mod)) : {}, __copyProps(
  // If the importer is in node compatibility mode or this is not an ESM
  // file that has been converted to a CommonJS file using a Babel-
  // compatible transform (i.e. "__esModule" has not been set), then set
  // "default" to the CommonJS "module.exports" for node compatibility.
  isNodeMode || !mod || !mod.__esModule ? __defProp(target, "default", { value: mod, enumerable: true }) : target,
  mod
));
var __toCommonJS = (mod) => __copyProps(__defProp({}, "__esModule", { value: true }), mod);

// src/index.ts
var index_exports = {};
__export(index_exports, {
  config: () => config,
  default: () => index_default,
  setup: () => setup2
});
module.exports = __toCommonJS(index_exports);

// src/jest.config.ts
var import_fs = __toESM(require("fs"));
var import_path = __toESM(require("path"));
var defaultSettings = {
  testEnvironment: "node",
  testEnvironmentOptions: {
    customExportConditions: ["react-native"]
  },
  preset: "react-native",
  transformIgnorePatterns: [],
  setupFilesAfterEnv: ["<rootDir>/jest.setup.js", "<rootDir>/jest.setup.ts"],
  testPathIgnorePatterns: ["/node_modules/", "/dist/", "/esm/"],
  testMatch: ["**/__tests__/**/*.[jt]s?(x)", "**/?(*.)+(spec|test).[jt]s?(x)"],
  moduleDirectories: ["node_modules", "src"],
  maxWorkers: 1
};
function settings(extraSettings) {
  const settings2 = {
    ...defaultSettings,
    ...extraSettings,
    setupFilesAfterEnv: handleSetupFilesAfterEnv(extraSettings),
    testMatch: handleTestMatch(extraSettings),
    displayName: handleDisplayName(extraSettings)
  };
  if (settings2.rootDir == null) {
    console.error(
      [
        `jest.config.js\uC5D0\uC11C rootDir\uB97C __dirname\uC73C\uB85C \uC124\uC815\uD574\uC8FC\uC138\uC694.`,
        "",
        "\uC608\uC2DC \uCF54\uB4DC:",
        "// jest.config.js",
        `module.exports = require('@granite-js/jest').config({`,
        `  rootDir: __dirname,`,
        `});`
      ].join("\n")
    );
  }
  return settings2;
}
function handleDisplayName(extraSettings) {
  if (extraSettings?.rootDir != null) {
    const packageJSONPath = import_path.default.resolve(extraSettings.rootDir, "package.json");
    if (import_fs.default.existsSync(packageJSONPath)) {
      return require(packageJSONPath).name;
    }
  }
  return void 0;
}
function handleTestMatch(extraSettings) {
  if (extraSettings?.rootDir != null && extraSettings.testMatch == null) {
    return [
      import_path.default.join(extraSettings.rootDir, "**/*.{spec,test}.{js,jsx,ts,tsx}"),
      import_path.default.join(extraSettings.rootDir, "**/__tests__/**/*.{js,jsx,ts,tsx}")
    ];
  }
  return defaultSettings.testMatch;
}
function handleSetupFilesAfterEnv(extraSettings) {
  const rootDir = extraSettings?.rootDir;
  if (rootDir != null) {
    const setupFilesAfterEnv = extraSettings?.setupFilesAfterEnv ?? defaultSettings.setupFilesAfterEnv;
    return setupFilesAfterEnv.map((file) => import_path.default.resolve(rootDir, file.replace("<rootDir>/", ""))).filter((filepath) => import_fs.default.existsSync(filepath));
  }
  return defaultSettings.setupFilesAfterEnv;
}
var jest_config_default = Object.assign(settings, defaultSettings);

// src/utils/requireFromRoot.ts
function requireFromRoot(id, rootDir) {
  const resolvedPath = require.resolve(id, { paths: [rootDir] });
  return require(resolvedPath);
}

// src/setup/reactNative.ts
function setupReactNative({ rootDir }) {
  setupGestureHandler({ rootDir });
  setupAppState();
  setupBridgeModule();
  setupWebView();
}
function setupGestureHandler({ rootDir }) {
  requireFromRoot("@granite-js/native/react-native-gesture-handler/jestSetup", rootDir);
  jest.useFakeTimers();
}
function setupAppState() {
  const ReactNative = require("react-native");
  ReactNative.AppState.currentState = "active";
}
function setupBridgeModule() {
  const ReactNative = require("react-native");
  ReactNative.NativeModules.GraniteModule = {
    addListener: jest.fn(),
    removeListeners: jest.fn()
  };
  ReactNative.NativeModules.GraniteCoreModule = {};
}
function setupWebView() {
  jest.mock("@granite-js/native/react-native-webview", () => ({ WebView: require("react-native").View }));
}

// src/setup/index.ts
var setup = setupReactNative;

// src/index.ts
var exported = {
  config: jest_config_default,
  setup
};
var setup2 = setup;
var config = jest_config_default;
var index_default = exported;
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  config,
  setup
});
