import { toConsumableArray as _toConsumableArray } from './_virtual/_rollupPluginBabelHelpers.mjs';

/** @tossdocs-ignore */

/**
 * @deprecated This feature is now available in the es-toolkit package.
 */
function uniq(arr) {
  return _toConsumableArray(new Set(arr));
}

export { uniq };
