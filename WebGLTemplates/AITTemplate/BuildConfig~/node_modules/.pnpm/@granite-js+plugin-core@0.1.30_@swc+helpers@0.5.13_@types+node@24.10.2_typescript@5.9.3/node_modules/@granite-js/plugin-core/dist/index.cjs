//#region rolldown:runtime
var __create = Object.create;
var __defProp = Object.defineProperty;
var __getOwnPropDesc = Object.getOwnPropertyDescriptor;
var __getOwnPropNames = Object.getOwnPropertyNames;
var __getProtoOf = Object.getPrototypeOf;
var __hasOwnProp = Object.prototype.hasOwnProperty;
var __copyProps = (to, from, except, desc) => {
	if (from && typeof from === "object" || typeof from === "function") for (var keys = __getOwnPropNames(from), i = 0, n = keys.length, key; i < n; i++) {
		key = keys[i];
		if (!__hasOwnProp.call(to, key) && key !== except) __defProp(to, key, {
			get: ((k) => from[k]).bind(null, key),
			enumerable: !(desc = __getOwnPropDesc(from, key)) || desc.enumerable
		});
	}
	return to;
};
var __toESM = (mod, isNodeMode, target) => (target = mod != null ? __create(__getProtoOf(mod)) : {}, __copyProps(isNodeMode || !mod || !mod.__esModule ? __defProp(target, "default", {
	value: mod,
	enumerable: true
}) : target, mod));

//#endregion
const es_toolkit = __toESM(require("es-toolkit"));
const path = __toESM(require("path"));
const __granite_js_utils = __toESM(require("@granite-js/utils"));
const fs = __toESM(require("fs"));
const zod = __toESM(require("zod"));
const cosmiconfig = __toESM(require("cosmiconfig"));
const cosmiconfig_typescript_loader = __toESM(require("cosmiconfig-typescript-loader"));

//#region src/createContext.ts
function createContext() {
	return { meta: Object.create(null) };
}

//#endregion
//#region src/utils/flattenPlugins.ts
const flattenPlugins = async (plugin) => {
	if (Array.isArray(plugin)) {
		const flattened = await Promise.all(plugin.map(async (p) => {
			if (p instanceof Promise) {
				const resolved$1 = await p;
				if (Array.isArray(resolved$1)) {
					const nested = await Promise.all(resolved$1.map(flattenPlugins));
					return nested.flat();
				}
				return flattenPlugins(resolved$1);
			}
			return flattenPlugins(p);
		}));
		return flattened.flat();
	}
	const resolved = await plugin;
	return [resolved];
};

//#endregion
//#region src/utils/resolvePlugins.ts
async function resolvePlugins(plugins) {
	const pluginsResolved = await flattenPlugins(plugins);
	return {
		plugins: pluginsResolved,
		configs: pluginsResolved.map((plugin) => plugin.config).filter(es_toolkit.isNotNil),
		pluginHooks: {
			devServer: {
				preHandlers: pluginsResolved.filter((plugin) => plugin.dev?.order === "pre").map((plugin) => plugin.dev?.handler),
				postHandlers: pluginsResolved.filter((plugin) => plugin.dev?.order === "post").map((plugin) => plugin.dev?.handler)
			},
			build: {
				preHandlers: pluginsResolved.filter((plugin) => plugin.build?.order === "pre").map((plugin) => plugin.build?.handler),
				postHandlers: pluginsResolved.filter((plugin) => plugin.build?.order === "post").map((plugin) => plugin.build?.handler)
			}
		}
	};
}

//#endregion
//#region src/utils/mergeBabel.ts
function mergeBabel(source, target) {
	if (!(source || target)) return void 0;
	if (source == null) return target;
	if (target == null) return source;
	return {
		...source,
		...target,
		presets: [...source.presets ?? [], ...target.presets ?? []],
		plugins: [...source.plugins ?? [], ...target.plugins ?? []],
		conditions: [...source.conditions ?? [], ...target.conditions ?? []]
	};
}

//#endregion
//#region src/utils/mergeDevServer.ts
function mergeDevServer(source, target) {
	if (!(source || target)) return void 0;
	if (source == null) return target;
	if (target == null) return source;
	return {
		...source,
		...target,
		middlewares: [...source.middlewares ?? [], ...target.middlewares ?? []]
	};
}

//#endregion
//#region src/utils/mergeEsbuild.ts
function mergeEsbuild(source, target) {
	if (!(source || target)) return void 0;
	if (source == null) return target;
	if (target == null) return source;
	return {
		...source,
		...target,
		define: {
			...source.define,
			...target.define
		},
		inject: [...source.inject || [], ...target.inject || []],
		prelude: [...source.prelude || [], ...target.prelude || []],
		banner: mergeBanners(source.banner ?? {}, target.banner ?? {})
	};
}
function mergeBanners(baseBanner, overrideBanner) {
	const result = { ...baseBanner };
	for (const [key, value] of Object.entries(overrideBanner)) result[key] = `${result[key] ?? ""}\n${value}`;
	return result;
}

//#endregion
//#region src/utils/mergeExtra.ts
function mergeExtra(source, target) {
	if (!(source || target)) return void 0;
	if (source == null) return target;
	if (target == null) return source;
	return {
		...source,
		...target
	};
}

//#endregion
//#region src/utils/mergeMetro.ts
function mergeMetro(source, target) {
	if (!(source || target)) return void 0;
	if (source == null) return target;
	if (target == null) return source;
	return {
		...source,
		...target,
		middlewares: [...source.middlewares || [], ...target.middlewares || []],
		resolver: mergeResolver$1(source.resolver, target.resolver),
		serializer: mergeSerializer(source.serializer, target.serializer),
		reporter: mergeReporter(source.reporter, target.reporter)
	};
}
function mergeResolver$1(source, target) {
	if (!(source || target)) return void 0;
	if (source == null) return target;
	if (target == null) return source;
	return {
		...source,
		...target,
		blockList: [...(Array.isArray(source.blockList) ? source.blockList : [source.blockList]) || [], ...(Array.isArray(target.blockList) ? target.blockList : [target.blockList]) || []].filter(es_toolkit.isNotNil)
	};
}
function mergeSerializer(source, target) {
	if (!(source || target)) return void 0;
	if (source == null) return target;
	if (target == null) return source;
	return {
		...source,
		...target,
		getPolyfills: () => [...source.getPolyfills?.() || [], ...target.getPolyfills?.() || []]
	};
}
function mergeReporter(source, target) {
	if (!(source || target)) return void 0;
	if (source == null) return target;
	if (target == null) return source;
	return { update: (event) => {
		source.update?.(event);
		target.update?.(event);
	} };
}

//#endregion
//#region src/utils/mergeResolver.ts
function mergeResolver(source, target) {
	if (!(source || target)) return void 0;
	if (source == null) return target;
	if (target == null) return source;
	return {
		alias: [...source.alias ?? [], ...target.alias ?? []],
		protocols: {
			...source.protocols,
			...target.protocols
		}
	};
}

//#endregion
//#region src/utils/mergeSwc.ts
function mergeSwc(source, target) {
	if (!(source || target)) return void 0;
	if (source == null) return target;
	if (target == null) return source;
	return {
		...source,
		...target,
		plugins: [...source.plugins || [], ...target.plugins || []]
	};
}

//#endregion
//#region src/utils/mergeTransformer.ts
function mergeTransformer(source, target) {
	if (!(source || target)) return void 0;
	if (source == null) return target;
	if (target == null) return source;
	let transformSync;
	let transformAsync;
	if (!(source.transformSync || target.transformSync)) transformSync = void 0;
	else if (source.transformSync && target.transformSync) transformSync = (id, code) => {
		let src = code;
		src = source.transformSync?.(id, src) ?? src;
		src = target.transformSync?.(id, src) ?? src;
		return src;
	};
	else if (source.transformSync) transformSync = source.transformSync;
	else if (target.transformSync) transformSync = target.transformSync;
	if (!(source.transformAsync || target.transformAsync)) transformAsync = void 0;
	else if (source.transformAsync && target.transformAsync) transformAsync = async (id, code) => {
		let src = code;
		src = await source.transformAsync?.(id, src) ?? src;
		src = await target.transformAsync?.(id, src) ?? src;
		return src;
	};
	else if (source.transformAsync) transformAsync = source.transformAsync;
	else if (target.transformAsync) transformAsync = target.transformAsync;
	return {
		transformSync,
		transformAsync
	};
}

//#endregion
//#region src/utils/mergeConfig.ts
async function mergeConfig(base, ...configs) {
	if (!(base || configs.length)) return void 0;
	return configs.reduce(async (acc, curr) => {
		const [resolvedAcc, resolvedCurr] = await Promise.all([acc, resolveDynamicConfig(curr)]);
		return {
			...resolvedAcc,
			...resolvedCurr,
			resolver: mergeResolver(resolvedAcc?.resolver, resolvedCurr?.resolver),
			transformer: mergeTransformer(resolvedAcc?.transformer, resolvedCurr?.transformer),
			esbuild: mergeEsbuild(resolvedAcc?.esbuild, resolvedCurr?.esbuild),
			babel: mergeBabel(resolvedAcc?.babel, resolvedCurr?.babel),
			swc: mergeSwc(resolvedAcc?.swc, resolvedCurr?.swc),
			devServer: mergeDevServer(resolvedAcc?.devServer, resolvedCurr?.devServer),
			metro: mergeMetro(resolvedAcc?.metro, resolvedCurr?.metro),
			extra: mergeExtra(resolvedAcc?.extra, resolvedCurr?.extra)
		};
	}, resolveDynamicConfig(base));
}
async function resolveDynamicConfig(config) {
	if (typeof config === "function") return await config();
	return config;
}

//#endregion
//#region src/utils/mergeBuildConfigs.ts
function mergeBuildConfigs(baseConfig, ...otherConfigs) {
	const mergedConfig = otherConfigs.reduce((acc, curr) => ({
		entry: acc.entry ?? curr.entry,
		outfile: acc.outfile ?? curr.outfile,
		platform: acc.platform ?? curr.platform,
		resolver: mergeResolver(acc.resolver, curr.resolver),
		transformer: mergeTransformer(acc.transformer, curr.transformer),
		esbuild: mergeEsbuild(acc.esbuild, curr.esbuild),
		swc: mergeSwc(acc.swc, curr.swc),
		babel: mergeBabel(acc.babel, curr.babel),
		extra: mergeExtra(acc.extra, curr.extra)
	}), baseConfig);
	return mergedConfig;
}

//#endregion
//#region src/utils/createPluginHooksDriver.ts
function createPluginHooksDriver(config) {
	const context = createPluginContext();
	const { devServer: devServerHooks, build: buildHooks } = config.pluginHooks;
	const baseArgs = {
		cwd: config.cwd,
		appName: config.appName,
		entryFile: config.entryFile,
		outdir: config.outdir
	};
	return {
		devServer: {
			pre: async (args) => {
				for (const handler of devServerHooks.preHandlers) await handler.call(context, {
					...baseArgs,
					...args
				});
			},
			post: async (args) => {
				for (const handler of devServerHooks.postHandlers) await handler.call(context, {
					...baseArgs,
					...args
				});
			}
		},
		build: {
			pre: async () => {
				for (const handler of buildHooks.preHandlers) await handler.call(context, { ...baseArgs });
			},
			post: async (args) => {
				for (const handler of buildHooks.postHandlers) await handler.call(context, {
					...baseArgs,
					...args
				});
			}
		}
	};
}
function createPluginContext() {
	const meta = Object.create(null);
	const context = { meta };
	return context;
}

//#endregion
//#region src/utils/buildResult.ts
function isBuildSuccess(result) {
	return "bundle" in result;
}
function isBuildFailure(result) {
	return !("bundle" in result);
}

//#endregion
//#region src/utils/resolveConfig.ts
const EMPTY_CONFIG = {};
async function resolveConfig(config) {
	const [base, ...rest] = config.pluginConfigs;
	if (base == null) return EMPTY_CONFIG;
	const mergedConfig = await mergeConfig(base, ...rest) ?? EMPTY_CONFIG;
	const resolvedConfig = {
		...mergedConfig,
		metro: resolveMetroConfig(mergedConfig)
	};
	return resolvedConfig;
}
/**
* Injects the some config into the metro config to ensure compatibility with the plugin config.
*/
function resolveMetroConfig(pluginConfig) {
	const metroConfig = pluginConfig.metro ?? {};
	return {
		...metroConfig,
		babelConfig: pluginConfig.babel,
		transformSync: pluginConfig?.transformer?.transformSync
	};
}

//#endregion
//#region src/config/graniteGlobals.ts
function prepareGraniteGlobalsScript(config) {
	const filePath = writeGraniteGlobalsScript(config);
	return {
		esbuild: { prelude: [filePath] },
		metro: { serializer: { getPolyfills: () => [filePath] } }
	};
}
function writeGraniteGlobalsScript(config) {
	const script = getGraniteGlobalScript(config);
	const filePath = path.default.join((0, __granite_js_utils.getLocalTempDirectoryPath)(config.rootDir), "granite-globals.js");
	(0, __granite_js_utils.prepareLocalDirectory)(config.rootDir);
	fs.default.writeFileSync(filePath, script, "utf-8");
	return filePath;
}
function getGraniteGlobalScript({ appName, scheme, host }) {
	return ["global.__granite = global.__granite || {};", `global.__granite.app = { name: ${JSON.stringify(appName)}, scheme: ${JSON.stringify(scheme)}, host: ${JSON.stringify(host)} };`].join("\n");
}

//#endregion
//#region src/schema/pluginConfig.ts
const pluginConfigSchema = zod.object({
	cwd: zod.string().default(process.cwd()),
	appName: zod.string(),
	host: zod.string().optional(),
	scheme: zod.string(),
	outdir: zod.string().default("dist"),
	entryFile: zod.string().default("./src/_app.tsx"),
	build: zod.custom().optional(),
	devServer: zod.custom().optional(),
	metro: zod.custom().optional(),
	plugins: zod.custom()
});

//#endregion
//#region src/config/defineConfig.ts
/**
* @public
* @category Configuration
* @name defineConfig
* @description
* Configures your Granite application by defining key settings in `granite.config.ts`.
*
* The configuration lets you specify:
* - How users will access your app through a URL scheme (e.g. `granite://`)
* - Your app's unique name that appears in the URL (e.g. `granite://my-service`)
* - Build settings for bundlers like ESBuild and Metro
* - Code transformation settings through Babel
* - Additional functionality through Granite plugins
*
* @param config - Configuration options
* @param config.cwd - Working directory for build process (defaults to process.cwd())
* @param config.appName - Your app's unique identifier
* @param config.host - Host name for your app (e.g. 'scheme://host/app-name')
* @param config.scheme - URL scheme for launching your app (e.g. 'granite')
* @param config.outdir - Where to output build files (defaults to 'dist')
* @param config.entryFile - Your app's entry point (defaults to './src/_app.tsx')
* @param config.build - Customize build settings
* @param config.metro - Configure Metro bundler settings
* @param config.devServer - Configure Mpack dev server settings
* @param config.plugins - Granite plugins to enhance functionality
* @returns The processed configuration
*
* @example
* Here's a basic configuration that:
* - Makes your app accessible via the `granite://` scheme
* - Names your service "my-app" so it's reachable at `granite://my-app`
* - Uses the Hermes plugin to optimize JavaScript bundles into bytecode
*
* ```ts
* import { defineConfig } from '@granite-js/react-native/config';
* import { hermes } from '@granite-js/plugin-hermes';
*
* export default defineConfig({
*   // The name of your microservice
*   appName: 'my-app',
*   // (Optional) The host name for your app (e.g. 'scheme://host/app-name')
*   host: 'super',
*   // The URL scheme for deep linking
*   scheme: 'granite',
*   // Entry file path
*   entryFile: 'index.ts',
*   // Array of plugins to use
*   plugins: [hermes()],
* });
* ```
*/
const defineConfig = async (config) => {
	const parsed = pluginConfigSchema.parse(config);
	const cwd = parsed.cwd ?? (0, __granite_js_utils.getPackageRoot)();
	const appName = parsed.appName;
	const host = parsed.host ?? "";
	const scheme = parsed.scheme;
	const entryFile = path.default.resolve(cwd, parsed.entryFile);
	const outdir = path.default.join(cwd, parsed.outdir);
	const parsedBuildConfig = parsed.build;
	const parsedDevServerConfig = parsed.devServer;
	const parsedMetroConfig = parsed.metro;
	const parsedConfig = {
		...parsedBuildConfig,
		devServer: parsedDevServerConfig,
		metro: parsedMetroConfig
	};
	const { configs, pluginHooks } = await resolvePlugins(parsed.plugins);
	const globalsScriptConfig = prepareGraniteGlobalsScript({
		rootDir: cwd,
		appName,
		scheme,
		host
	});
	return {
		cwd,
		appName,
		entryFile,
		outdir,
		pluginHooks,
		pluginConfigs: [
			parsedConfig,
			globalsScriptConfig,
			...configs
		].filter(es_toolkit.isNotNil)
	};
};

//#endregion
//#region src/config/loadConfig.ts
const MODULE_NAME = "granite";
const loadConfig = async (options = {}) => {
	let result;
	const resolveRoot = options.root ?? (0, __granite_js_utils.getPackageRoot)();
	if (options.configFile) result = await getConfigExplorer().load(path.default.resolve(resolveRoot, options.configFile));
	else result = await getConfigExplorer({ searchPlaces: [
		`${MODULE_NAME}.config.ts`,
		`${MODULE_NAME}.config.mts`,
		`${MODULE_NAME}.config.js`,
		`${MODULE_NAME}.config.cjs`
	] }).search(resolveRoot);
	(0, es_toolkit.assert)(result, "Config file not found");
	const config = await result.config;
	return config;
};
function getConfigExplorer(options) {
	return (0, cosmiconfig.cosmiconfig)(MODULE_NAME, {
		loaders: {
			".ts": (0, cosmiconfig_typescript_loader.TypeScriptLoader)(),
			".mts": (0, cosmiconfig_typescript_loader.TypeScriptLoader)()
		},
		...options
	});
}

//#endregion
exports.createContext = createContext;
exports.createPluginContext = createPluginContext;
exports.createPluginHooksDriver = createPluginHooksDriver;
exports.defineConfig = defineConfig;
exports.flattenPlugins = flattenPlugins;
exports.isBuildFailure = isBuildFailure;
exports.isBuildSuccess = isBuildSuccess;
exports.loadConfig = loadConfig;
exports.mergeBuildConfigs = mergeBuildConfigs;
exports.mergeConfig = mergeConfig;
exports.pluginConfigSchema = pluginConfigSchema;
exports.resolveConfig = resolveConfig;
exports.resolvePlugins = resolvePlugins;