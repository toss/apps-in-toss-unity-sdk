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
var debuggerPlugin_exports = {};
__export(debuggerPlugin_exports, {
  debuggerPlugin: () => debuggerPlugin
});
module.exports = __toCommonJS(debuggerPlugin_exports);
var import_static = __toESM(require("@fastify/static"));
var import_devtools_frontend = __toESM(require("@granite-js/devtools-frontend"));
var import_fastify_plugin = __toESM(require("fastify-plugin"));
var import_constants = require("../../constants");
var import_logger = require("../../logger");
async function debuggerPluginImpl(app, config) {
  import_logger.logger.debug("debugger-plugin", { root: import_devtools_frontend.default, prefix: import_constants.DEBUGGER_FRONTEND_PATH });
  app.register(import_static.default, {
    root: import_devtools_frontend.default,
    prefix: import_constants.DEBUGGER_FRONTEND_PATH
  });
  app.route({
    method: ["GET", "POST"],
    url: "/open-debugger",
    handler: async (request, reply) => {
      import_logger.logger.trace("open-debugger-plugin", { body: request.body });
      reply.status(404);
    }
  }).route({
    method: ["GET", "POST"],
    url: "/reload",
    handler: async (_request, reply) => {
      import_logger.logger.trace("debugger-plugin");
      config.onReload();
      reply.status(200).send("OK");
    }
  });
}
const debuggerPlugin = (0, import_fastify_plugin.default)(debuggerPluginImpl, {
  name: "debugger-plugin"
});
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  debuggerPlugin
});
