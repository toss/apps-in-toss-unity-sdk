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
const fs = __toESM(require("fs"));
const path = __toESM(require("path"));
const __granite_js_utils = __toESM(require("@granite-js/utils"));
const picocolors = __toESM(require("picocolors"));

//#region src/prelude.ts
function getPreludeConfig(options) {
	const sharedEntries = Object.entries(options.shared ?? {});
	const eagerEntries = sharedEntries.filter(([_, config]) => config.eager === true);
	const registerStatements = eagerEntries.map(([libName], index) => {
		const identifier = `__mod${index}`;
		return `
    // ${libName}
    import * as ${identifier} from '${libName}';
    registerShared('${libName}', ${identifier});
    `;
	});
	const exposeStatements = Object.entries(options.exposes ?? {}).map(([exposeName, modulePath], index) => {
		const identifier = `__expose${index}`;
		const resolvedModulePath = path.default.resolve(modulePath);
		return `
    import * as ${identifier} from '${resolvedModulePath}';
    exposeModule(__container, '${exposeName}', ${identifier});
    `;
	});
	const preludeScript = [
		`import { registerShared, createContainer, exposeModule } from '@granite-js/plugin-micro-frontend/runtime';`,
		`const __container = createContainer('${options.name}', ${JSON.stringify({
			remote: options.remote,
			shared: options.shared
		})});`,
		...registerStatements,
		...exposeStatements
	].join("\n");
	return {
		banner: `
    if (global.__MICRO_FRONTEND__ == null) {
      global.__MICRO_FRONTEND__ = {
        __SHARED__: {},
        __INSTANCES__: [],
      };
    }
    `,
		preludeScript
	};
}

//#endregion
//#region src/log.ts
const tag = picocolors.default.bold(picocolors.default.bgCyan(picocolors.default.black(" MICRO FRONTEND ")));
function log(...args) {
	console.log(tag, ...args);
}

//#endregion
//#region src/remote.ts
const FALLBACK_SCRIPT = `console.warn('[MICRO FRONTEND] Failed to fetch remote bundles. Please check if the remote dev server is running')`;
async function fetchRemoteBundle(remote) {
	globalThis.remoteBundles = {
		android: FALLBACK_SCRIPT,
		ios: FALLBACK_SCRIPT
	};
	try {
		log("Prefetching remote bundles for development environment...");
		const [androidBundle, iosBundle] = await Promise.all([fetchBundle(remote, "android"), fetchBundle(remote, "ios")]);
		globalThis.remoteBundles = {
			android: androidBundle,
			ios: iosBundle
		};
		log("Fetch complete");
	} catch {
		log("Failed to fetch remote bundles. Please check if the remote dev server is running");
	}
}
async function fetchBundle(remote, platform) {
	const response = await fetch(`http://${remote.host}:${remote.port}/index.bundle?dev=true&platform=${platform}`);
	const bundle = await response.text();
	return bundle;
}

//#endregion
//#region src/utils/resolveReactNativeBasePath.ts
function resolveReactNativeBasePath() {
	return path.default.dirname(require.resolve("react-native/package.json", { paths: [(0, __granite_js_utils.getPackageRoot)()] }));
}

//#endregion
//#region src/resolver.ts
const VIRTUAL_INITIALIZE_CORE_PROTOCOL = "virtual-initialize-core";
const VIRTUAL_SHARED_PROTOCOL = "virtual-shared";
function virtualInitializeCoreConfig(reactNativeBasePath = resolveReactNativeBasePath()) {
	const initializeCorePath = path.join(reactNativeBasePath, "Libraries/Core/InitializeCore.js");
	const alias = [{
		from: `prelude:${initializeCorePath}`,
		to: `${VIRTUAL_INITIALIZE_CORE_PROTOCOL}:noop`,
		exact: false
	}];
	const protocols = { [VIRTUAL_INITIALIZE_CORE_PROTOCOL]: { load: function virtualInitializeCoreProtocolLoader() {
		return {
			loader: "js",
			contents: `// noop`
		};
	} } };
	return {
		alias,
		protocols
	};
}
function virtualSharedConfig(moduleEntries) {
	const alias = moduleEntries.map(([libName]) => ({
		from: libName,
		to: `${VIRTUAL_SHARED_PROTOCOL}:${libName}`,
		exact: true
	}));
	const protocols = { [VIRTUAL_SHARED_PROTOCOL]: { load: function virtualSharedProtocolLoader(args) {
		return {
			loader: "js",
			contents: `
          var sharedModule = global.__MICRO_FRONTEND__.__SHARED__['${args.path}'];

          if (sharedModule == null) {
            throw new Error("'${args.path}' is not registered in the shared registry");
          }

          module.exports = sharedModule.get();
          `
		};
	} } };
	return {
		alias,
		protocols: alias.length > 0 ? protocols : void 0
	};
}

//#endregion
//#region src/utils/intoShared.ts
function intoShared(shared) {
	if (Array.isArray(shared)) return shared.reduce((acc, lib) => {
		acc[lib] = {};
		return acc;
	}, {});
	return shared;
}

//#endregion
//#region src/microFrontendPlugin.ts
const microFrontendPlugin = async (options) => {
	const sharedEntries = Object.entries(intoShared(options.shared) ?? {});
	const nonEagerEntries = sharedEntries.filter(([_, config]) => config.eager !== true);
	const rootDir = process.cwd();
	const preludeConfig = getPreludeConfig(options);
	const localDir = (0, __granite_js_utils.prepareLocalDirectory)(rootDir);
	const preludePath = path.join(localDir, "micro-frontend-runtime.js");
	fs.writeFileSync(preludePath, preludeConfig.preludeScript);
	/**
	* @TODO `MPACK_DEV_SERVER` flag should be removed after next version of bundle loader is released and load bundle dynamically at JS runtime.
	*/
	if (process.env.MPACK_DEV_SERVER === "true" && options.remote) await fetchRemoteBundle(options.remote);
	/**
	* If importing `react-native` from the shared registry,
	* `InitializeCore.js` must be excluded from the bundle to ensure the core is loaded only once per runtime.
	*/
	const shouldExcludeReactNativeInitializeCore = Boolean(nonEagerEntries.find(([libName]) => libName === "react-native"));
	const virtualInitializeCore = shouldExcludeReactNativeInitializeCore ? virtualInitializeCoreConfig(options.reactNativeBasePath) : void 0;
	const virtualShared = virtualSharedConfig(nonEagerEntries);
	return {
		name: "micro-frontend-plugin",
		config: {
			resolver: {
				alias: [...virtualInitializeCore?.alias ?? [], ...virtualShared.alias],
				protocols: {
					...virtualInitializeCore?.protocols,
					...virtualShared.protocols
				}
			},
			esbuild: {
				prelude: [preludePath],
				banner: { js: preludeConfig.banner }
			}
		}
	};
};

//#endregion
exports.microFrontend = microFrontendPlugin;