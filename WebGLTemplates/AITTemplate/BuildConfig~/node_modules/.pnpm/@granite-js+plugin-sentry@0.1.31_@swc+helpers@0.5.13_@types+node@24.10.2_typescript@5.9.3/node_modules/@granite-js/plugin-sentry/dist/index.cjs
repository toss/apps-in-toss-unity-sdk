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
const fs_promises = __toESM(require("fs/promises"));
const __granite_js_plugin_core = __toESM(require("@granite-js/plugin-core"));
const es_toolkit = __toESM(require("es-toolkit"));
const crypto = __toESM(require("crypto"));
const fs = __toESM(require("fs"));
const __sentry_cli = __toESM(require("@sentry/cli"));

//#region src/snippets.ts
const SENTRY_DEBUG_ID_HOLDER = "SENTRY_DEBUG_ID";
function getSentryDebugIdSnippets() {
	const debugId = (0, crypto.randomUUID)();
	const debugIdHolder = `${SENTRY_DEBUG_ID_HOLDER}=${debugId}`;
	const sourceMappingComment = `//# debugId=${debugId}`;
	const injectionScript = `
  // ${debugIdHolder}
  try {
    var globalObject =
      'undefined' != typeof window
        ? window
        : 'undefined' != typeof global
          ? global
          : 'undefined' != typeof self
            ? self
            : {};

    var stack = new Error().stack;

    if (stack) {
      globalObject._sentryDebugIds = globalObject._sentryDebugIds || {};
      globalObject._sentryDebugIds[stack] = '${debugId}';
      globalObject._sentryDebugIdIdentifier = 'sentry-dbid-${debugId}';
    }
  } catch (e) {}
  `;
	return {
		injectionScript,
		sourceMappingComment
	};
}

//#endregion
//#region src/extractSentryDebugId.ts
function extractSentryDebugId(bundleContent) {
	return bundleContent.match(new RegExp(`${SENTRY_DEBUG_ID_HOLDER}=([\\w-]+)`))?.[1];
}

//#endregion
//#region src/injectSentryDebugId.ts
async function writeDebugIdInjectedSourcemap(sourcemapPath, debugId) {
	const sourcemapObject = await injectSentryDebugId(sourcemapPath, debugId);
	await fs_promises.writeFile(sourcemapPath, JSON.stringify(sourcemapObject, null, 2), "utf-8");
}
async function injectSentryDebugId(sourcemapPath, debugId) {
	const sourcemapContent = await fs_promises.readFile(sourcemapPath, "utf-8");
	const sourcemapObject = JSON.parse(sourcemapContent);
	sourcemapObject.debugId = debugId;
	return sourcemapObject;
}

//#endregion
//#region src/resolveHermesBundle.ts
function tryResolveHermesBundle(jsBundlePath) {
	const lookupPath = jsBundlePath.replace(/\.js$/, ".hbc");
	return fs.existsSync(lookupPath) ? {
		hbc: lookupPath,
		sourcemap: `${lookupPath}.map`
	} : null;
}

//#endregion
//#region src/uploadSourcemap.ts
function uploadSourcemap(context, { bundlePath, sourcemapPath }) {
	return context.client.execute([
		"sourcemaps",
		"upload",
		"--debug-id-reference",
		"--strip-prefix",
		context.root,
		bundlePath,
		sourcemapPath
	], true);
}

//#endregion
//#region src/sentryClientActions.ts
function createClientActions(options) {
	if (options.enabled === false || options.useClient === false) return { uploadSourcemap: es_toolkit.asyncNoop };
	const client = new __sentry_cli.default(null, options);
	return { uploadSourcemap: async (context, options$1) => {
		await uploadSourcemap({
			client,
			...context
		}, options$1);
	} };
}

//#endregion
//#region src/sentryPlugin.ts
const PLUGIN_NAME = "sentry-plugin";
const PLUGIN_SHIM = { name: PLUGIN_NAME };
const sentryPlugin = ({ enabled = true,...options } = {}) => {
	if (enabled === false) return PLUGIN_SHIM;
	const sentryActions = createClientActions(options);
	return {
		name: PLUGIN_NAME,
		build: {
			order: "post",
			handler: async function(config) {
				const sentryResults = [];
				const files = config.buildResults.filter(__granite_js_plugin_core.isBuildSuccess).map(({ outfile, sourcemapOutfile }) => ({
					bundle: outfile,
					sourcemap: sourcemapOutfile
				}));
				for (const file of files) {
					const { bundle, sourcemap } = file;
					const bundleContent = await fs_promises.readFile(bundle, "utf-8");
					const debugId = await extractSentryDebugId(bundleContent);
					if (debugId == null) {
						console.error("Cannot find Sentry Debug ID");
						continue;
					}
					const hermesBundle = tryResolveHermesBundle(bundle);
					const targetBundle = hermesBundle?.hbc ?? bundle;
					const targetSourcemap = hermesBundle?.sourcemap ?? sourcemap;
					await Promise.all([writeDebugIdInjectedSourcemap(sourcemap, debugId), hermesBundle?.sourcemap ? writeDebugIdInjectedSourcemap(hermesBundle?.sourcemap, debugId) : es_toolkit.noop]);
					await sentryActions.uploadSourcemap({ root: config.cwd }, {
						bundlePath: targetBundle,
						sourcemapPath: targetSourcemap
					});
					sentryResults.push({
						bundle: targetBundle,
						sourcemap: targetSourcemap,
						debugId
					});
				}
				this.meta.sentry = sentryResults;
			}
		},
		config: () => {
			const { injectionScript, sourceMappingComment } = getSentryDebugIdSnippets();
			return { esbuild: {
				banner: { js: injectionScript },
				footer: { js: sourceMappingComment }
			} };
		}
	};
};

//#endregion
exports.sentry = sentryPlugin;