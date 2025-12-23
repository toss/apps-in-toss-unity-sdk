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
var serveBundlePlugin_exports = {};
__export(serveBundlePlugin_exports, {
  serveBundlePlugin: () => serveBundlePlugin
});
module.exports = __toCommonJS(serveBundlePlugin_exports);
var import_path = __toESM(require("path"));
var import_fastify_plugin = __toESM(require("fastify-plugin"));
var import_reply = require("./reply");
var import_logger = require("../../logger");
var import_stripExtension = require("../../utils/stripExtension");
var import_constants = require("../constants");
var BundleResourceType = /* @__PURE__ */ ((BundleResourceType2) => {
  BundleResourceType2["Bundle"] = "bundle";
  BundleResourceType2["Sourcemap"] = "Sourcemap";
  return BundleResourceType2;
})(BundleResourceType || {});
async function _serveBundlePlugin(app, { getBundle }) {
  app.route({
    method: ["GET"],
    url: "/:file",
    schema: {
      querystring: {
        type: "object",
        properties: {
          platform: {
            type: "string"
          },
          app: {
            type: "string"
          },
          dev: {
            type: "boolean"
          },
          minify: {
            type: "boolean"
          },
          runModule: {
            type: "boolean"
          },
          inlineSourceMap: {
            type: "boolean"
          },
          modulesOnly: {
            type: "boolean"
          }
        },
        required: ["platform"]
      }
    },
    handler: async (request, reply) => {
      const { file } = request.params;
      const query = request.query;
      const { platform } = query;
      import_logger.logger.trace("serve-bundle-plugin", query);
      if (file == null) {
        return (0, import_reply.invalidRequest)(reply, "file is required");
      }
      const bundleResourceType = file.endsWith(".bundle") ? "bundle" /* Bundle */ : file.endsWith(".map") ? "Sourcemap" /* Sourcemap */ : null;
      const isInvalidResourceType = bundleResourceType == null;
      const isInvalidPlatform = !(platform === "android" || platform === "ios");
      if (isInvalidResourceType || isInvalidPlatform) {
        return (0, import_reply.invalidRequest)(reply, JSON.stringify({ isInvalidResourceType, platform }));
      }
      if (!(platform === "android" || platform === "ios")) {
        return (0, import_reply.invalidRequest)(reply, "invalid platform");
      }
      const { name } = import_path.default.parse(file);
      if ((0, import_stripExtension.stripExtension)(name) !== import_constants.DEV_SERVER_BUNDLE_NAME) {
        return (0, import_reply.notFound)(reply, `invalid bundle name: ${name}`);
      }
      const bundle = await getBundle(platform);
      switch (bundleResourceType) {
        case "bundle" /* Bundle */:
          return reply.headers({
            "Content-Type": "application/javascript; charset=UTF-8"
            // TODO: Custom header injection
          }).status(200).send(Buffer.from(bundle.source.contents).toString("utf-8"));
        case "Sourcemap" /* Sourcemap */:
          return reply.header("Access-Control-Allow-Origin", "devtools://devtools").status(200).send(Buffer.from(bundle.sourcemap.contents).toString("utf-8"));
      }
    }
  });
}
const serveBundlePlugin = (0, import_fastify_plugin.default)(_serveBundlePlugin, {
  name: "serve-bundle-plugin"
});
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  serveBundlePlugin
});
