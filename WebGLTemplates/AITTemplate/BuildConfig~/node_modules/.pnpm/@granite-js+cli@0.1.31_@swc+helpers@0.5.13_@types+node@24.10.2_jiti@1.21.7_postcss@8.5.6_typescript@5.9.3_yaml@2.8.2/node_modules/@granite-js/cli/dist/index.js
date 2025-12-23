import { createRequire } from 'module'; const require = createRequire(import.meta.url);
var __require = /* @__PURE__ */ ((x) => typeof require !== "undefined" ? require : typeof Proxy !== "undefined" ? new Proxy(x, {
  get: (a, b) => (typeof require !== "undefined" ? require : a)[b]
}) : x)(function(x) {
  if (typeof require !== "undefined") return require.apply(this, arguments);
  throw Error('Dynamic require of "' + x + '" is not supported');
});

// src/index.ts
import { Builtins, Cli } from "clipanion";

// src/commands/BuildCommand/BuildCommand.ts
import { BuildUtils } from "@granite-js/mpack";
import { statusPlugin } from "@granite-js/mpack/plugins";
import { loadConfig } from "@granite-js/plugin-core";
import { Command, Option } from "clipanion";

// src/utils/command.ts
import chalk from "chalk";
function errorHandler(error) {
  const label = chalk.red("Error");
  if (error instanceof Error) {
    console.error(label, error.message);
  } else {
    console.error(label, "Unknown error", (error ?? "")?.toString());
  }
  return 1 /* ERROR */;
}

// src/commands/BuildCommand/BuildCommand.ts
var BuildCommand = class extends Command {
  static paths = [[`build`]];
  static usage = Command.Usage({
    category: "Build",
    description: "Build Granite App",
    examples: [["Build Granite App", "granite build"]]
  });
  configFile = Option.String("--config", {
    description: "Path to config file"
  });
  dev = Option.Boolean("--dev", {
    description: "Build in development mode"
  });
  metafile = Option.Boolean("--metafile", {
    description: "Generate metafile"
  });
  cache = Option.Boolean("--cache", {
    description: "Enable cache"
  });
  async execute() {
    try {
      const { configFile, cache = true, metafile = false, dev = false } = this;
      const config = await loadConfig({ configFile });
      const options = ["android", "ios"].map((platform) => ({
        dev,
        cache,
        metafile,
        platform,
        outfile: `bundle.${platform}.js`
      }));
      await BuildUtils.buildAll(options, { config, plugins: [statusPlugin] });
      return 0 /* SUCCESS */;
    } catch (error) {
      return errorHandler(error);
    }
  }
};

// src/commands/HermesCommand/HermesCommand.ts
import path2 from "path";
import chalk2 from "chalk";
import { Command as Command2, Option as Option2 } from "clipanion";

// src/utils/compileHbc.ts
import assert from "assert";
import os from "os";
import path from "path";
import { isNotNil } from "es-toolkit";
import execa from "execa";
var binary = {
  Darwin: "react-native/sdks/hermesc/osx-bin/hermesc",
  Linux: "react-native/sdks/hermesc/linux64-bin/hermesc",
  Windows_NT: "react-native/sdks/hermesc/win64-bin/hermesc.exe"
};
async function compileHbc({ rootDir, filePath, sourcemap }) {
  const binary2 = getHermesc(rootDir);
  const outfile = path.resolve(rootDir, filePath.replace(new RegExp(`${path.extname(filePath)}$`), ".hbc"));
  await execa(
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
    ].filter(isNotNil)
  );
  return { outfile, sourcemapOutfile: sourcemap ? `${outfile}.map` : null };
}
function getHermesc(rootDir) {
  const os2 = getOs();
  const binarySource = binary[os2];
  assert(binarySource, `\uC9C0\uC6D0\uD558\uC9C0 \uC54A\uB294 OS \uC785\uB2C8\uB2E4: ${os2}`);
  return resolveFromRoot(rootDir, binarySource);
}
function getOs() {
  return os.type();
}
function resolveFromRoot(rootDir, request) {
  return __require.resolve(request, { paths: [rootDir] });
}

// src/commands/HermesCommand/HermesCommand.ts
var HermesCommand = class extends Command2 {
  static paths = [[`hermes`]];
  static usage = Command2.Usage({
    category: "Hermes",
    description: "\uC9C0\uC815\uD55C \uBC88\uB4E4\uC744 Hermes \uBC14\uC774\uD2B8 \uCF54\uB4DC\uB85C \uCEF4\uD30C\uC77C \uD569\uB2C8\uB2E4",
    examples: [["\uCEF4\uD30C\uC77C\uD558\uAE30", "granite hermes --jsbundle dist/bundle.js"]]
  });
  jsBundleFile = Option2.String("--jsbundle", {
    required: true,
    description: "Hermes \uBC14\uC774\uD2B8 \uCF54\uB4DC\uB85C \uCEF4\uD30C\uC77C \uD560 Javascript \uD30C\uC77C \uACBD\uB85C \uC785\uB2C8\uB2E4"
  });
  sourcemap = Option2.Boolean("--sourcemap", true, {
    description: "\uC18C\uC2A4\uB9F5 \uD30C\uC77C\uC744 \uC0DD\uC131\uD569\uB2C8\uB2E4"
  });
  async execute() {
    try {
      const rootDir = process.cwd();
      const filePath = path2.resolve(rootDir, this.jsBundleFile);
      const { outfile, sourcemapOutfile } = await compileHbc({ rootDir, filePath, sourcemap: this.sourcemap });
      console.log(`\u2705 Compiled successfully: ${chalk2.gray(outfile)}`);
      if (sourcemapOutfile) {
        console.log(`\u2705 Source map generated successfully: ${chalk2.gray(sourcemapOutfile)}`);
      }
      return 0 /* SUCCESS */;
    } catch (error) {
      return errorHandler(error);
    }
  }
};

// src/commands/DevCommand/DevCommand.ts
import { runServer, EXPERIMENTAL__server } from "@granite-js/mpack";
import { loadConfig as loadConfig2 } from "@granite-js/plugin-core";
import { Command as Command3, Option as Option3 } from "clipanion";
import Debug from "debug";
var debug = Debug("cli");
var DevCommand = class extends Command3 {
  static paths = [[`dev`]];
  static usage = Command3.Usage({
    category: "Development",
    description: "Run Granite development server",
    examples: [["Run Granite development server", "granite dev"]]
  });
  configFile = Option3.String("--config", {
    description: "Path to config file"
  });
  host = Option3.String("--host");
  port = Option3.String("--port");
  disableEmbeddedReactDevTools = Option3.Boolean("--disable-embedded-react-devtools", false);
  // mpack dev-server
  experimentalMode = Option3.Boolean("--experimental-mode");
  async execute() {
    try {
      process.env.MPACK_DEV_SERVER = "true";
      const config = await loadConfig2({ configFile: this.configFile });
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
        await EXPERIMENTAL__server({ config, ...serverOptions });
      } else {
        await runServer({
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
var cli = new Cli({
  binaryLabel: "granite",
  binaryName: "granite",
  enableCapture: true
});
async function initialize() {
  cli.register(BuildCommand);
  cli.register(HermesCommand);
  cli.register(DevCommand);
  cli.register(Builtins.HelpCommand);
  cli.runExit(process.argv.slice(2));
}
export {
  initialize
};
