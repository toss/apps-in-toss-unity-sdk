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
var DevServer_exports = {};
__export(DevServer_exports, {
  DevServer: () => DevServer
});
module.exports = __toCommonJS(DevServer_exports);
var import_assert = __toESM(require("assert"));
var import_fastify = __toESM(require("fastify"));
var import_backend = require("react-native-devtools-standalone/backend");
var import_DebuggerEventHandler = require("./debugger/DebuggerEventHandler");
var import_createBundlerForDevServer = require("./helpers/createBundlerForDevServer");
var import_mergeBundles = require("./helpers/mergeBundles");
var import_middlewares = require("./middlewares");
var serverPlugins = __toESM(require("./plugins"));
var import_wss = require("./wss");
var import_constants = require("../constants");
var import_logger = require("../logger");
var import_statusPlugin = require("../plugins/statusPlugin");
var import_isDebugMode = require("../utils/isDebugMode");
var import_progressBar = require("../utils/progressBar");
var import_dev_middleware = require("../vendors/@react-native/dev-middleware");
var import_cli_server_api = require("../vendors/@react-native-community/cli-server-api");
class DevServer {
  constructor(devServerOptions) {
    this.devServerOptions = devServerOptions;
    import_logger.logger.trace("DevServer.constructor");
    this.host = devServerOptions.host ?? import_constants.DEV_SERVER_DEFAULT_HOST;
    this.port = devServerOptions.port ?? import_constants.DEV_SERVER_DEFAULT_PORT;
    process.env.DEV_SERVER_HOST = String(this.host);
    process.env.DEV_SERVER_PORT = String(this.port);
    const app = (0, import_fastify.default)({
      logger: {
        level: (0, import_isDebugMode.isDebugMode)("mpack") ? "trace" : "silent"
      }
    });
    this.app = app;
    this.setup(app);
  }
  host;
  port;
  app;
  context = null;
  inspectorProxy;
  wssDelegate;
  async initialize() {
    import_logger.logger.trace("DevServer.initialize");
    const { rootDir, buildConfig } = this.devServerOptions;
    this.context = await this.createDevServerContext(rootDir, buildConfig);
  }
  listen() {
    import_logger.logger.trace("DevServer.listen");
    return this.app.listen({ host: this.host, port: this.port }).then(() => {
      import_logger.logger.info(`\uAC1C\uBC1C \uC11C\uBC84 \uC2E4\uD589 \uC911 - ${this.getBaseUrl()}`);
    });
  }
  close() {
    return this.app.close();
  }
  getInspectorProxy() {
    return this.inspectorProxy;
  }
  getBaseUrl() {
    return `http://${this.host}:${this.port}`;
  }
  broadcastCommand(command) {
    this.wssDelegate?.broadcastCommand?.(command);
  }
  getContext() {
    (0, import_assert.default)(this.context, "\uCD08\uAE30\uD654\uAC00 \uC644\uB8CC\uB418\uC9C0 \uC54A\uC558\uC2B5\uB2C8\uB2E4");
    return this.context;
  }
  async setup(app) {
    const baseRoot = this.devServerOptions.rootDir;
    const serverBaseUrl = this.getBaseUrl();
    const debuggerEventHandler = new import_DebuggerEventHandler.DebuggerEventHandler(this.devServerOptions.inspectorProxy?.delegate);
    const inspectorProxy = new import_dev_middleware.InspectorProxy({ root: baseRoot, serverBaseUrl });
    const inspectorProxyWss = inspectorProxy.createWebSocketServers({
      onDeviceWebSocketConnected: (socket) => {
        debuggerEventHandler.setDeviceWebSocketHandler(socket);
      },
      onDebuggerWebSocketConnected: (socket) => {
        debuggerEventHandler.setDebuggerWebSocketHandler(socket);
      }
    });
    const { debuggerProxySocket, eventsSocket, messageSocket } = (0, import_cli_server_api.createWebSocketEndpoints)({
      broadcast: (command, params) => {
        this.wssDelegate?.broadcastCommand(command, params);
      }
    });
    const liveReloadMiddleware = (0, import_middlewares.createLiveReloadMiddleware)({
      onClientLog: (event) => {
        this.wssDelegate?.sendEvent(event);
        if (event.type === "client_log") {
          (0, import_logger.clientLogger)(event.level, event.data);
        }
      }
    });
    const wssDelegate = new import_wss.WebSocketServerDelegate({
      eventReporter: (event) => eventsSocket.reportEvent(event),
      messageBroadcaster: (command, params) => messageSocket.broadcast(command, params),
      hmr: {
        updateStart: () => liveReloadMiddleware.updateStart(),
        updateDone: () => liveReloadMiddleware.updateDone(),
        reload: () => liveReloadMiddleware.liveReload()
      }
    });
    app.register(serverPlugins.statusPlugin, { rootDir: this.devServerOptions.rootDir }).register(serverPlugins.debuggerPlugin, { onReload: () => this.wssDelegate?.broadcastCommand("reload") }).register(serverPlugins.serveBundlePlugin, { getBundle: this.getBundle.bind(this) }).register(serverPlugins.symbolicatePlugin, { getBundle: this.getBundle.bind(this) }).register(serverPlugins.indexPagePlugin).addHook("onRequest", inspectorProxy.handleRequest).addHook("onSend", this.setCommonHeaders);
    for (const plugin of this.devServerOptions.middlewares ?? []) {
      app.register(plugin);
    }
    new import_wss.WebSocketServerRouter().register("/hot", liveReloadMiddleware.server).register("/debugger-proxy", debuggerProxySocket.server).register("/message", messageSocket.server).register("/events", eventsSocket.server).register("/inspector/device", inspectorProxyWss.deviceSocketServer).register("/inspector/debug", inspectorProxyWss.debuggerSocketServer).setup(app);
    await (0, import_backend.setupDevToolsProxy)({
      client: {
        delegate: {
          onError: (error) => import_logger.logger.error("React DevTools client error", error)
        }
      },
      devtools: {
        delegate: {
          onError: (error) => import_logger.logger.error("React DevTools frontend error", error)
        }
      }
    });
    this.inspectorProxy = inspectorProxy;
    this.wssDelegate = wssDelegate;
  }
  setCommonHeaders(_request, reply, _payload, next) {
    reply.header("Surrogate-Control", "no-store");
    reply.header("Cache-Control", "no-store, no-cache, must-revalidate, proxy-revalidate");
    reply.header("Pragma", "no-cache");
    reply.header("Expires", "0");
    next();
  }
  async createDevServerContext(rootDir, buildConfig) {
    const [androidBundler, iosBundler] = await Promise.all([
      (0, import_createBundlerForDevServer.createBundlerForDevServer)({ rootDir, platform: "android", buildConfig }),
      (0, import_createBundlerForDevServer.createBundlerForDevServer)({ rootDir, platform: "ios", buildConfig })
    ]);
    [androidBundler, iosBundler].forEach((bundler) => {
      bundler.addPlugin(
        (0, import_statusPlugin.statusPlugin)({
          onStart: () => {
            this.wssDelegate?.onHMRUpdateStart();
          },
          onEnd: () => {
            this.wssDelegate?.onHMRUpdateDone();
            this.wssDelegate?.hotReload();
          }
        })
      );
    });
    return {
      rootDir,
      android: {
        bundler: androidBundler,
        progressBar: (0, import_progressBar.createProgressBar)("android")
      },
      ios: {
        bundler: iosBundler,
        progressBar: (0, import_progressBar.createProgressBar)("ios")
      }
    };
  }
  async getBundle(platform) {
    const { bundler } = this.getContext()[platform];
    const buildResult = await bundler.build({ withDispose: false });
    let targetBundle;
    if ("bundle" in buildResult) {
      if (globalThis.remoteBundles != null) {
        const hostBundleContent = buildResult.bundle.source.text;
        const remoteBundleContent = globalThis.remoteBundles[platform];
        const mergedBundle = await (0, import_mergeBundles.mergeBundles)({
          platform,
          hostBundleContent,
          remoteBundleContent
        });
        targetBundle = mergedBundle;
      } else {
        targetBundle = buildResult.bundle;
      }
      return targetBundle;
    } else {
      throw new Error("Build failed");
    }
  }
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  DevServer
});
