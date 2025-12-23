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
  initialize: () => initialize
});
module.exports = __toCommonJS(index_exports);
var import_clipanion4 = require("clipanion");

// src/commands/BuildCommand/BuildCommand.ts
var import_mpack = require("@granite-js/mpack");
var import_plugins = require("@granite-js/mpack/plugins");
var import_plugin_core = require("@granite-js/plugin-core");
var import_clipanion = require("clipanion");

// src/utils/command.ts
var import_chalk = __toESM(require("chalk"), 1);
function errorHandler(error) {
  const label = import_chalk.default.red("Error");
  if (error instanceof Error) {
    console.error(label, error.message);
  } else {
    console.error(label, "Unknown error", (error ?? "")?.toString());
  }
  return 1 /* ERROR */;
}

// src/commands/BuildCommand/BuildCommand.ts
var BuildCommand = class extends import_clipanion.Command {
  static paths = [[`build`]];
  static usage = import_clipanion.Command.Usage({
    category: "Build",
    description: "Build Granite App",
    examples: [["Build Granite App", "granite build"]]
  });
  configFile = import_clipanion.Option.String("--config", {
    description: "Path to config file"
  });
  dev = import_clipanion.Option.Boolean("--dev", {
    description: "Build in development mode"
  });
  metafile = import_clipanion.Option.Boolean("--metafile", {
    description: "Generate metafile"
  });
  cache = import_clipanion.Option.Boolean("--cache", {
    description: "Enable cache"
  });
  async execute() {
    try {
      const { configFile, cache = true, metafile = false, dev = false } = this;
      const config = await (0, import_plugin_core.loadConfig)({ configFile });
      const options = ["android", "ios"].map((platform) => ({
        dev,
        cache,
        metafile,
        platform,
        outfile: `bundle.${platform}.js`
      }));
      await import_mpack.BuildUtils.buildAll(options, { config, plugins: [import_plugins.statusPlugin] });
      return 0 /* SUCCESS */;
    } catch (error) {
      return errorHandler(error);
    }
  }
};

// src/commands/HermesCommand/HermesCommand.ts
var import_path2 = __toESM(require("path"), 1);
var import_chalk2 = __toESM(require("chalk"), 1);
var import_clipanion2 = require("clipanion");

// src/utils/compileHbc.ts
var import_assert = __toESM(require("assert"), 1);
var import_os = __toESM(require("os"), 1);
var import_path = __toESM(require("path"), 1);
var import_es_toolkit = require("es-toolkit");
var import_execa = __toESM(require("execa"), 1);
var binary = {
  Darwin: "react-native/sdks/hermesc/osx-bin/hermesc",
  Linux: "react-native/sdks/hermesc/linux64-bin/hermesc",
  Windows_NT: "react-native/sdks/hermesc/win64-bin/hermesc.exe"
};
async function compileHbc({ rootDir, filePath, sourcemap }) {
  const binary2 = getHermesc(rootDir);
  const outfile = import_path.default.resolve(rootDir, filePath.replace(new RegExp(`${import_path.default.extname(filePath)}$`), ".hbc"));
  await (0, import_execa.default)(
    binary2,
    [
      // Disable warnings
      "-w",
      // Expensive optimizations
      "-O",
      // Emit binary
      "-emit-binary",
      // Emit source map
      sourcemap ? "-output-source-map" : null,
      // Output path
      "-out",
      outfile,
      filePath
    ].filter(import_es_toolkit.isNotNil)
  );
  return { outfile, sourcemapOutfile: sourcemap ? `${outfile}.map` : null };
}
function getHermesc(rootDir) {
  const os2 = getOs();
  const binarySource = binary[os2];
  (0, import_assert.default)(binarySource, `\uC9C0\uC6D0\uD558\uC9C0 \uC54A\uB294 OS \uC785\uB2C8\uB2E4: ${os2}`);
  return resolveFromRoot(rootDir, binarySource);
}
function getOs() {
  return import_os.default.type();
}
function resolveFromRoot(rootDir, request) {
  return require.resolve(request, { paths: [rootDir] });
}

// src/commands/HermesCommand/HermesCommand.ts
var HermesCommand = class extends import_clipanion2.Command {
  static paths = [[`hermes`]];
  static usage = import_clipanion2.Command.Usage({
    category: "Hermes",
    description: "\uC9C0\uC815\uD55C \uBC88\uB4E4\uC744 Hermes \uBC14\uC774\uD2B8 \uCF54\uB4DC\uB85C \uCEF4\uD30C\uC77C \uD569\uB2C8\uB2E4",
    examples: [["\uCEF4\uD30C\uC77C\uD558\uAE30", "granite hermes --jsbundle dist/bundle.js"]]
  });
  jsBundleFile = import_clipanion2.Option.String("--jsbundle", {
    required: true,
    description: "Hermes \uBC14\uC774\uD2B8 \uCF54\uB4DC\uB85C \uCEF4\uD30C\uC77C \uD560 Javascript \uD30C\uC77C \uACBD\uB85C \uC785\uB2C8\uB2E4"
  });
  sourcemap = import_clipanion2.Option.Boolean("--sourcemap", true, {
    description: "\uC18C\uC2A4\uB9F5 \uD30C\uC77C\uC744 \uC0DD\uC131\uD569\uB2C8\uB2E4"
  });
  async execute() {
    try {
      const rootDir = process.cwd();
      const filePath = import_path2.default.resolve(rootDir, this.jsBundleFile);
      const { outfile, sourcemapOutfile } = await compileHbc({ rootDir, filePath, sourcemap: this.sourcemap });
      console.log(`\u2705 Compiled successfully: ${import_chalk2.default.gray(outfile)}`);
      if (sourcemapOutfile) {
        console.log(`\u2705 Source map generated successfully: ${import_chalk2.default.gray(sourcemapOutfile)}`);
      }
      return 0 /* SUCCESS */;
    } catch (error) {
      return errorHandler(error);
    }
  }
};

// src/commands/DevCommand/DevCommand.ts
var import_mpack2 = require("@granite-js/mpack");
var import_plugin_core2 = require("@granite-js/plugin-core");
var import_clipanion3 = require("clipanion");
var import_debug = __toESM(require("debug"), 1);
var debug = (0, import_debug.default)("cli");
var DevCommand = class extends import_clipanion3.Command {
  static paths = [[`dev`]];
  static usage = import_clipanion3.Command.Usage({
    category: "Development",
    description: "Run Granite development server",
    examples: [["Run Granite development server", "granite dev"]]
  });
  configFile = import_clipanion3.Option.String("--config", {
    description: "Path to config file"
  });
  host = import_clipanion3.Option.String("--host");
  port = import_clipanion3.Option.String("--port");
  disableEmbeddedReactDevTools = import_clipanion3.Option.Boolean("--disable-embedded-react-devtools", false);
  // mpack dev-server
  experimentalMode = import_clipanion3.Option.Boolean("--experimental-mode");
  async execute() {
    try {
      process.env.MPACK_DEV_SERVER = "true";
      const config = await (0, import_plugin_core2.loadConfig)({ configFile: this.configFile });
      const serverOptions = {
        host: this.host,
        port: this.port ? parseInt(this.port, 10) : void 0
      };
      debug("StartCommand", {
        ...serverOptions,
        disableEmbeddedReactDevTools: this.disableEmbeddedReactDevTools,
        experimentalMode: this.experimentalMode
      });
      if (this.experimentalMode) {
        await (0, import_mpack2.EXPERIMENTAL__server)({ config, ...serverOptions });
      } else {
        await (0, import_mpack2.runServer)({
          config,
          enableEmbeddedReactDevTools: !this.disableEmbeddedReactDevTools,
          ...serverOptions
        });
      }
      return 0 /* SUCCESS */;
    } catch (error) {
      return errorHandler(error);
    }
  }
};

// src/index.ts
var cli = new import_clipanion4.Cli({
  binaryLabel: "granite",
  binaryName: "granite",
  enableCapture: true
});
async function initialize() {
  cli.register(BuildCommand);
  cli.register(HermesCommand);
  cli.register(DevCommand);
  cli.register(import_clipanion4.Builtins.HelpCommand);
  cli.runExit(process.argv.slice(2));
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  initialize
});
