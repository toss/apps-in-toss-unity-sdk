"use strict";
var __create = Object.create;
var __defProp = Object.defineProperty;
var __getOwnPropDesc = Object.getOwnPropertyDescriptor;
var __getOwnPropNames = Object.getOwnPropertyNames;
var __getProtoOf = Object.getPrototypeOf;
var __hasOwnProp = Object.prototype.hasOwnProperty;
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
var fs = __toESM(require("fs"));
var path = __toESM(require("path"));
var import_ws = __toESM(require("ws"));
const debug = require("debug")("Metro:InspectorProxy");
const PAGES_POLLING_INTERVAL = 1e3;
const EMULATOR_LOCALHOST_ADDRESSES = ["10.0.2.2", "10.0.3.2"];
const FILE_PREFIX = "file://";
const REACT_NATIVE_RELOADABLE_PAGE_ID = "-1";
class Device {
  // ID of the device.
  _id;
  // Name of the device.
  _name;
  // Package name of the app.
  _app;
  // Stores socket connection between Inspector Proxy and device.
  _deviceSocket;
  // Stores last list of device's pages.
  _pages;
  // Stores information about currently connected debugger (if any).
  _debuggerConnection = null;
  // Last known Page ID of the React Native page.
  // This is used by debugger connections that don't have PageID specified
  // (and will interact with the latest React Native page).
  _lastConnectedReactNativePage = null;
  // Whether we are in the middle of a reload in the REACT_NATIVE_RELOADABLE_PAGE.
  _isReloading = false;
  // The previous "GetPages" message, for deduplication in debug logs.
  _lastGetPagesMessage = "";
  // Mapping built from scriptParsed events and used to fetch file content in `Debugger.getScriptSource`.
  _scriptIdToSourcePathMapping = /* @__PURE__ */ new Map();
  // Root of the project used for relative to absolute source path conversion.
  _projectRoot;
  // MARK: - GRANITE
  _delegate;
  // MARK: - GRANITE
  // 네트워크 응답 데이터 저장하기 위한 변수 (key: requestId, value: { data: string, base64Encoded: bool })
  _networkResponseData = /* @__PURE__ */ new Map();
  constructor(id, name, app, socket, projectRoot, delegate) {
    this._id = id;
    this._name = name;
    this._app = app;
    this._pages = [];
    this._deviceSocket = socket;
    this._projectRoot = projectRoot;
    this._delegate = delegate;
    this._deviceSocket.on("message", (message) => {
      const parsedMessage = JSON.parse(message);
      if (parsedMessage.event === "getPages") {
        if (message !== this._lastGetPagesMessage) {
          debug("(Debugger)    (Proxy) <- (Device), getPages ping has changed: " + message);
          this._lastGetPagesMessage = message;
        }
      } else {
        debug("(Debugger)    (Proxy) <- (Device): " + message);
      }
      this._handleMessageFromDevice(parsedMessage);
    });
    this._deviceSocket.on("close", () => {
      if (this._debuggerConnection) {
        this._debuggerConnection.socket.close();
        this._debuggerConnection = null;
      }
    });
    this._setPagesPolling();
  }
  getName() {
    return this._name;
  }
  getPagesList() {
    if (this._lastConnectedReactNativePage) {
      const reactNativeReloadablePage = {
        id: REACT_NATIVE_RELOADABLE_PAGE_ID,
        title: "React Native Experimental (Improved Chrome Reloads)",
        vm: "don't use",
        app: this._app
      };
      return this._pages.concat(reactNativeReloadablePage);
    } else {
      return this._pages;
    }
  }
  // Handles new debugger connection to this device:
  // 1. Sends connect event to device
  // 2. Forwards all messages from the debugger to device as wrappedEvent
  // 3. Sends disconnect event to device when debugger connection socket closes.
  handleDebuggerConnection(socket, pageId) {
    if (this._debuggerConnection) {
      this._debuggerConnection.socket.close();
      this._debuggerConnection = null;
    }
    const debuggerInfo = {
      socket,
      prependedFilePrefix: false,
      pageId
    };
    this._debuggerConnection = debuggerInfo;
    debug(`Got new debugger connection for page ${pageId} of ${this._name}`);
    this._sendMessageToDevice({
      event: "connect",
      payload: {
        pageId: this._mapToDevicePageId(pageId)
      }
    });
    socket.on("message", (message) => {
      if (!this._lastConnectedReactNativePage) {
        return;
      }
      debug("(Debugger) -> (Proxy)    (Device): " + message);
      const debuggerRequest = JSON.parse(message);
      const interceptedResponse = this._interceptMessageFromDebugger(debuggerRequest, debuggerInfo);
      if (interceptedResponse) {
        socket.send(JSON.stringify(interceptedResponse));
      } else {
        this._sendMessageToDevice({
          event: "wrappedEvent",
          payload: {
            pageId: this._mapToDevicePageId(pageId),
            wrappedEvent: JSON.stringify(debuggerRequest)
          }
        });
      }
    });
    socket.on("close", () => {
      debug(`Debugger for page ${pageId} and ${this._name} disconnected.`);
      this._sendMessageToDevice({
        event: "disconnect",
        payload: {
          pageId: this._mapToDevicePageId(pageId)
        }
      });
      this._debuggerConnection = null;
    });
    const sendFunc = socket.send;
    socket.send = function(message) {
      debug("(Debugger) <- (Proxy)    (Device): " + message);
      return sendFunc.call(socket, message);
    };
  }
  // Handles messages received from device:
  // 1. For getPages responses updates local _pages list.
  // 2. All other messages are forwarded to debugger as wrappedEvent.
  //
  // In the future more logic will be added to this method for modifying
  // some of the messages (like updating messages with source maps and file
  // locations).
  _handleMessageFromDevice(message) {
    if (message.event === "getPages") {
      this._pages = message.payload;
      for (let i = 0; i < this._pages.length; ++i) {
        if (this._pages[i].title.indexOf("React") >= 0) {
          if (this._pages[i].id != this._lastConnectedReactNativePage?.id) {
            this._newReactNativePage(this._pages[i]);
            break;
          }
        }
      }
    } else if (message.event === "disconnect") {
      const pageId = message.payload.pageId;
      const debuggerSocket = this._debuggerConnection ? this._debuggerConnection.socket : null;
      if (debuggerSocket && debuggerSocket.readyState === import_ws.default.OPEN) {
        if (this._debuggerConnection != null && this._debuggerConnection.pageId !== REACT_NATIVE_RELOADABLE_PAGE_ID) {
          debug(`Page ${pageId} is reloading.`);
          debuggerSocket.send(JSON.stringify({ method: "reload" }));
        }
      }
    } else if (message.event === "wrappedEvent") {
      if (this._debuggerConnection == null) {
        return;
      }
      const debuggerSocket = this._debuggerConnection.socket;
      if (debuggerSocket == null || debuggerSocket.readyState !== import_ws.default.OPEN) {
        return;
      }
      const parsedPayload = JSON.parse(message.payload.wrappedEvent);
      if (this._debuggerConnection) {
        this._processMessageFromDevice(parsedPayload, this._debuggerConnection);
      }
      const messageToSend = JSON.stringify(parsedPayload);
      debuggerSocket.send(messageToSend);
    }
  }
  // Sends single message to device.
  _sendMessageToDevice(message) {
    try {
      if (message.event !== "getPages") {
        debug("(Debugger)    (Proxy) -> (Device): " + JSON.stringify(message));
      }
      this._deviceSocket.send(JSON.stringify(message));
    } catch (error) {
    }
  }
  // Sends 'getPages' request to device every PAGES_POLLING_INTERVAL milliseconds.
  _setPagesPolling() {
    setInterval(() => this._sendMessageToDevice({ event: "getPages" }), PAGES_POLLING_INTERVAL);
  }
  // We received new React Native Page ID.
  _newReactNativePage(page) {
    debug(`React Native page updated to ${page.id}`);
    if (this._debuggerConnection == null || this._debuggerConnection.pageId !== REACT_NATIVE_RELOADABLE_PAGE_ID) {
      this._lastConnectedReactNativePage = page;
      return;
    }
    const oldPageId = this._lastConnectedReactNativePage?.id;
    this._lastConnectedReactNativePage = page;
    this._isReloading = true;
    if (oldPageId != null) {
      this._sendMessageToDevice({
        event: "disconnect",
        payload: {
          pageId: oldPageId
        }
      });
    }
    this._sendMessageToDevice({
      event: "connect",
      payload: {
        pageId: page.id
      }
    });
    const toSend = [
      { method: "Runtime.enable", id: 1e9 },
      { method: "Debugger.enable", id: 1e9 }
    ];
    for (const message of toSend) {
      this._sendMessageToDevice({
        event: "wrappedEvent",
        payload: {
          pageId: this._mapToDevicePageId(page.id),
          wrappedEvent: JSON.stringify(message)
        }
      });
    }
  }
  // Allows to make changes in incoming message from device.
  _processMessageFromDevice(payload, debuggerInfo) {
    if (this._delegate?.onDeviceMessage?.(payload, debuggerInfo.socket)) {
      return;
    }
    if (payload.method === "Debugger.scriptParsed") {
      const params = payload.params || {};
      if ("sourceMapURL" in params) {
        for (let i = 0; i < EMULATOR_LOCALHOST_ADDRESSES.length; ++i) {
          const address = EMULATOR_LOCALHOST_ADDRESSES[i];
          if (params.sourceMapURL.indexOf(address) >= 0) {
            payload.params.sourceMapURL = params.sourceMapURL.replace(address, "localhost");
            debuggerInfo.originalSourceURLAddress = address;
          }
        }
      }
      if ("url" in params) {
        for (let i = 0; i < EMULATOR_LOCALHOST_ADDRESSES.length; ++i) {
          const address = EMULATOR_LOCALHOST_ADDRESSES[i];
          if (params.url.indexOf(address) >= 0) {
            payload.params.url = params.url.replace(address, "localhost");
            debuggerInfo.originalSourceURLAddress = address;
          }
        }
        if (payload.params.url.match(/^[0-9a-z]+$/)) {
          payload.params.url = FILE_PREFIX + payload.params.url;
          debuggerInfo.prependedFilePrefix = true;
        }
        if (params.scriptId != null) {
          this._scriptIdToSourcePathMapping.set(params.scriptId, params.url);
        }
      }
      if (debuggerInfo.pageId == REACT_NATIVE_RELOADABLE_PAGE_ID) {
        if (payload.params.sourceMapURL) {
          payload.params.sourceMapURL += "&cachePrevention=" + this._mapToDevicePageId(debuggerInfo.pageId);
        }
        if (payload.params.url) {
          payload.params.url += "&cachePrevention=" + this._mapToDevicePageId(debuggerInfo.pageId);
        }
      }
    }
    if (payload.method === "Runtime.executionContextCreated" && this._isReloading) {
      debuggerInfo.socket.send(JSON.stringify({ method: "Runtime.executionContextsCleared" }));
      this._sendMessageToDevice({
        event: "wrappedEvent",
        payload: {
          pageId: this._mapToDevicePageId(debuggerInfo.pageId),
          wrappedEvent: JSON.stringify({ method: "Debugger.resume", id: 0 })
        }
      });
      this._isReloading = false;
    }
    if (payload.method === "Bedrock.networkResponseData" || payload.method === "Granite.networkResponseData") {
      const params = payload.params ?? {};
      if (typeof params.requestId === "string") {
        this._networkResponseData.set(params.requestId, {
          data: params.data,
          base64Encoded: params.base64Encoded
        });
      }
    }
  }
  // Allows to make changes in incoming messages from debugger.
  _interceptMessageFromDebugger(req, debuggerInfo) {
    let response = null;
    if (this._delegate?.onDebuggerMessage?.(req, debuggerInfo.socket)) {
      return null;
    }
    if (req.method === "Debugger.setBreakpointByUrl") {
      this._processDebuggerSetBreakpointByUrl(req, debuggerInfo);
    } else if (req.method === "Debugger.getScriptSource") {
      response = {
        id: req.id,
        result: this._processDebuggerGetScriptSource(req)
      };
    } else if (req.method === "Network.getResponseBody") {
      response = this._processDebuggerGetResponseBody(req, debuggerInfo.socket);
    }
    return response;
  }
  // MARK: - GRANITE
  _processDebuggerGetResponseBody(req) {
    const { requestId } = req.params;
    if (this._networkResponseData.has(requestId)) {
      const responseData = this._networkResponseData.get(requestId);
      this._networkResponseData.delete(requestId);
      return {
        id: req.id,
        result: this._createNetworkResponseData(responseData)
      };
    }
    return null;
  }
  // MARK: - GRANITE
  _createNetworkResponseData(responseData) {
    let parsedOriginalData;
    try {
      parsedOriginalData = JSON.parse(responseData.base64Encoded ? atob(responseData.data) : responseData.data);
    } catch {
      return {
        body: responseData.data,
        base64Encoded: responseData.base64Encoded
      };
    }
    const body = typeof parsedOriginalData === "object" ? JSON.stringify(parsedOriginalData) : responseData.data;
    return { body, base64Encoded: false };
  }
  _processDebuggerSetBreakpointByUrl(req, debuggerInfo) {
    if (debuggerInfo.originalSourceURLAddress) {
      if (req.params.url) {
        req.params.url = req.params.url.replace("localhost", debuggerInfo.originalSourceURLAddress);
        if (req.params.url && req.params.url.startsWith(FILE_PREFIX) && debuggerInfo.prependedFilePrefix) {
          req.params.url = req.params.url.slice(FILE_PREFIX.length);
        }
      }
      if (req.params.urlRegex) {
        req.params.urlRegex = req.params.urlRegex.replace(
          /localhost/g,
          // $FlowFixMe[incompatible-call]
          debuggerInfo.originalSourceURLAddress
        );
      }
    }
  }
  _processDebuggerGetScriptSource(req) {
    let scriptSource = `Source for script with id '${req.params.scriptId}' was not found.`;
    const pathToSource = this._scriptIdToSourcePathMapping.get(req.params.scriptId);
    if (pathToSource) {
      try {
        scriptSource = fs.readFileSync(path.resolve(this._projectRoot, pathToSource), "utf8");
      } catch (err) {
        scriptSource = err.message;
      }
    }
    return {
      scriptSource
    };
  }
  _mapToDevicePageId(pageId) {
    if (pageId === REACT_NATIVE_RELOADABLE_PAGE_ID && this._lastConnectedReactNativePage != null) {
      return this._lastConnectedReactNativePage.id;
    } else {
      return pageId;
    }
  }
}
module.exports = Device;
