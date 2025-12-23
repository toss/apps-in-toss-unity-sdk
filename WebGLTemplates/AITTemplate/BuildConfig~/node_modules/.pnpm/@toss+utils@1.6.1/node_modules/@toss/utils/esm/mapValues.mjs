import { slicedToArray as _slicedToArray } from './_virtual/_rollupPluginBabelHelpers.mjs';

/** @tossdocs-ignore */

/**
 * @deprecated This feature is now available in the es-toolkit package.
 */
function mapValues(value, mapper) {
  var entries = Object.entries(value);
  return Object.fromEntries(entries.map(function (_ref) {
    var _ref2 = _slicedToArray(_ref, 2),
        k = _ref2[0],
        v = _ref2[1];

    return [k, mapper(v)];
  }));
}

export { mapValues };
