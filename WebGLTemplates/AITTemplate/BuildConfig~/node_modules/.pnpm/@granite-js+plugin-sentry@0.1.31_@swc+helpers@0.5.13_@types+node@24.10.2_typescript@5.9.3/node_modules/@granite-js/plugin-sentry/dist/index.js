import * as fs$2 from "fs/promises";
import * as fs from "fs/promises";
import { isBuildSuccess } from "@granite-js/plugin-core";
import { asyncNoop, noop } from "es-toolkit";
import { randomUUID } from "crypto";
import * as fs$1 from "fs";
import SentryCLI from "@sentry/cli";

//#region src/snippets.ts
const SENTRY_DEBUG_ID_HOLDER = "SENTRY_DEBUG_ID";
function getSentryDebugIdSnippets() {
	const debugId = randomUUID();
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
	await fs$2.writeFile(sourcemapPath, JSON.stringify(sourcemapObject, null, 2), "utf-8");
}
async function injectSentryDebugId(sourcemapPath, debugId) {
	const sourcemapContent = await fs$2.readFile(sourcemapPath, "utf-8");
	const sourcemapObject = JSON.parse(sourcemapContent);
	sourcemapObject.debugId = debugId;
	return sourcemapObject;
}

//#endregion
//#region src/resolveHermesBundle.ts
function tryResolveHermesBundle(jsBundlePath) {
	const lookupPath = jsBundlePath.replace(/\.js$/, ".hbc");
	return fs$1.existsSync(lookupPath) ? {
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
	if (options.enabled === false || options.useClient === false) return { uploadSourcemap: asyncNoop };
	const client = new SentryCLI(null, options);
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
				const files = config.buildResults.filter(isBuildSuccess).map(({ outfile, sourcemapOutfile }) => ({
					bundle: outfile,
					sourcemap: sourcemapOutfile
				}));
				for (const file of files) {
					const { bundle, sourcemap } = file;
					const bundleContent = await fs.readFile(bundle, "utf-8");
					const debugId = await extractSentryDebugId(bundleContent);
					if (debugId == null) {
						console.error("Cannot find Sentry Debug ID");
						continue;
					}
					const hermesBundle = tryResolveHermesBundle(bundle);
					const targetBundle = hermesBundle?.hbc ?? bundle;
					const targetSourcemap = hermesBundle?.sourcemap ?? sourcemap;
					await Promise.all([writeDebugIdInjectedSourcemap(sourcemap, debugId), hermesBundle?.sourcemap ? writeDebugIdInjectedSourcemap(hermesBundle?.sourcemap, debugId) : noop]);
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
export { sentryPlugin as sentry };