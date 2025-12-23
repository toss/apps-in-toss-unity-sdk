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
var serve_exports = {};
__export(serve_exports, {
  runServer: () => runServer
});
module.exports = __toCommonJS(serve_exports);
var import_plugin_core = require("@granite-js/plugin-core");
var import_cli_server_api = require("@react-native-community/cli-server-api");
var import_debug = __toESM(require("debug"));
var import_backend = require("react-native-devtools-standalone/backend");
var import_createDebuggerMiddleware = require("./createDebuggerMiddleware");
var import_constants = require("../constants");
var import_getMetroConfig = require("../metro/getMetroConfig");
var import_printLogo = require("../utils/printLogo");
var import_printServerUrl = require("../utils/printServerUrl");
var import_vendors = require("../vendors");
const debug = (0, import_debug.default)("cli:start");
const { Metro, TerminalReporter } = (0, import_vendors.getModule)("metro");
const { Terminal } = (0, import_vendors.getModule)("metro-core");
const { mergeConfig } = (0, import_vendors.getModule)("metro-config");
async function runServer({
  config,
  host = import_constants.DEV_SERVER_DEFAULT_HOST,
  port = import_constants.DEV_SERVER_DEFAULT_PORT,
  enableEmbeddedReactDevTools = true,
  onServerReady
}) {
  const ref = {};
  const driver = (0, import_plugin_core.createPluginHooksDriver)(config);
  const terminal = new Terminal(process.stdout);
  const terminalReporter = new TerminalReporter(terminal);
  const reporter = {
    async update(event) {
      debug("Reporter event", event);
      terminalReporter.update(event);
      ref.reportEvent?.(event);
      if (baseConfig.reporter?.update) {
        baseConfig.reporter.update(event);
      }
      switch (event.type) {
        case "initialize_started":
          (0, import_printLogo.printLogo)();
          break;
        case "initialize_done":
          enableStdinWatchMode();
          await driver.devServer.post({ host, port });
          (0, import_printServerUrl.printServerUrl)({ host, port });
          await onServerReady?.();
          break;
        default:
          break;
      }
    }
  };
  const resolvedConfig = await (0, import_plugin_core.resolveConfig)(config);
  const { middlewares = [], inspectorProxy, ...additionalMetroConfig } = resolvedConfig?.metro ?? {};
  const baseConfig = await (0, import_getMetroConfig.getMetroConfig)({ rootPath: config.cwd }, additionalMetroConfig);
  const metroConfig = mergeConfig(baseConfig, {
    server: { port },
    reporter
  });
  const { middleware, websocketEndpoints, messageSocketEndpoint, eventsSocketEndpoint } = (0, import_cli_server_api.createDevServerMiddleware)({
    host,
    port,
    watchFolders: metroConfig.watchFolders
  });
  const { middleware: debuggerMiddleware, enableStdinWatchMode } = (0, import_createDebuggerMiddleware.createDebuggerMiddleware)({
    port,
    broadcastMessage: messageSocketEndpoint.broadcast
  });
  middleware.use(debuggerMiddleware);
  middleware.use(import_cli_server_api.indexPageMiddleware);
  const customEnhanceMiddleware = metroConfig.server.enhanceMiddleware;
  metroConfig.server.enhanceMiddleware = (metroMiddleware, server) => {
    if (customEnhanceMiddleware) {
      metroMiddleware = customEnhanceMiddleware(metroMiddleware, server);
    }
    for (const item of middlewares) {
      middleware.use(item);
    }
    return middleware.use(metroMiddleware);
  };
  if (enableEmbeddedReactDevTools) {
    await (0, import_backend.setupDevToolsProxy)({
      client: {
        delegate: {
          onError: (error) => console.error("React DevTools client error", error)
        }
      },
      devtools: {
        delegate: {
          onError: (error) => console.error("React DevTools frontend error", error)
        }
      }
    });
  }
  ref.reportEvent = eventsSocketEndpoint.reportEvent;
  ref.enableStdinWatchMode = enableStdinWatchMode;
  await driver.devServer.pre({ host, port });
  const serverInstance = await Metro.runServer(metroConfig, {
    host,
    websocketEndpoints,
    inspectorProxyDelegate: inspectorProxy?.delegate
  });
  serverInstance.keepAliveTimeout = 3e4;
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  runServer
});
