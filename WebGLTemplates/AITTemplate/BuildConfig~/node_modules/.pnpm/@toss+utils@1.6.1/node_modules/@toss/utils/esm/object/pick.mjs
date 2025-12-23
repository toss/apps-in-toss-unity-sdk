import { createForOfIteratorHelper as _createForOfIteratorHelper } from '../_virtual/_rollupPluginBabelHelpers.mjs';

/** @tossdocs-ignore */

/**
 * @deprecated This feature is now available in the es-toolkit package.
 */
function pick(obj, keys) {
  var picked = {};

  var _iterator = _createForOfIteratorHelper(keys),
      _step;

  try {
    for (_iterator.s(); !(_step = _iterator.n()).done;) {
      var key = _step.value;
      picked[key] = obj[key];
    }
  } catch (err) {
    _iterator.e(err);
  } finally {
    _iterator.f();
  }

  return picked;
}

export { pick };
