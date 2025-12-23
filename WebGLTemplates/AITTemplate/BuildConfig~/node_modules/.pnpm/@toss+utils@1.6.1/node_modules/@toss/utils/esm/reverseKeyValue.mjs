import { slicedToArray as _slicedToArray } from './_virtual/_rollupPluginBabelHelpers.mjs';

/** @tossdocs-ignore */

/**
 * @deprecated This feature is now available in the es-toolkit package as `invert`.
 */
function reverseKeyValue(obj) {
  return Object.fromEntries(Object.entries(obj).map(function (_ref) {
    var _ref2 = _slicedToArray(_ref, 2),
        key = _ref2[0],
        value = _ref2[1];

    return [value, key];
  }));
}

export { reverseKeyValue };
