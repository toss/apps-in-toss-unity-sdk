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
var symbolicatePlugin_exports = {};
__export(symbolicatePlugin_exports, {
  symbolicatePlugin: () => symbolicatePlugin
});
module.exports = __toCommonJS(symbolicatePlugin_exports);
var url = __toESM(require("url"));
var import_fastify_plugin = __toESM(require("fastify-plugin"));
var import_parseStackFrame = require("./parseStackFrame");
var import_symbolicate = require("./symbolicate");
var import_logger = require("../../../logger");
var import_stripExtension = require("../../../utils/stripExtension");
var import_constants = require("../../constants");
var import_reply = require("../reply");
async function symbolicatePluginImpl(app, { getBundle }) {
  app.route({
    method: ["GET", "POST"],
    url: "/symbolicate",
    handler: async (request, reply) => {
      import_logger.logger.debug("symbolicate-plugin", { body: request.body });
      const stackFrame = (0, import_parseStackFrame.parseStackFrame)(request.body);
      if (stackFrame == null) {
        return (0, import_reply.invalidRequest)(reply, "invalid stack frame data");
      }
      const devBundlePath = stackFrame.find(({ file }) => file.startsWith("http"));
      if (devBundlePath == null) {
        return (0, import_reply.notFound)(reply, "no sourcemap found");
      }
      const {
        pathname,
        query: { platform }
      } = url.parse(devBundlePath.file, true);
      if ((0, import_stripExtension.stripExtension)(pathname ?? "") !== import_constants.DEV_SERVER_BUNDLE_NAME) {
        return (0, import_reply.notFound)(reply, `invalid bundle name: ${pathname}`);
      }
      if (!(platform === "android" || platform === "ios")) {
        return (0, import_reply.invalidRequest)(reply, "invalid platform");
      }
      const { sourcemap } = await getBundle(platform);
      return reply.status(200).send(await (0, import_symbolicate.symbolicate)(sourcemap.contents, stackFrame));
    }
  });
}
const symbolicatePlugin = (0, import_fastify_plugin.default)(symbolicatePluginImpl, {
  name: "symbolicate-plugin"
});
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  symbolicatePlugin
});
