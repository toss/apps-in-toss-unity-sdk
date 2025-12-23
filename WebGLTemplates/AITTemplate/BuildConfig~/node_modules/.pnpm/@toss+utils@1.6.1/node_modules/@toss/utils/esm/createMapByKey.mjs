import { createForOfIteratorHelper as _createForOfIteratorHelper } from './_virtual/_rollupPluginBabelHelpers.mjs';

/** @tossdocs-ignore */

/**
 * @deprecated This feature is now available in the es-toolkit package as `keyBy`.
 */
function createMapByKey(objects, key) {
  var map = new Map();

  var _iterator = _createForOfIteratorHelper(objects),
      _step;

  try {
    for (_iterator.s(); !(_step = _iterator.n()).done;) {
      var obj = _step.value;
      var keyValue = obj[key];
      map.set(keyValue, obj);
    }
  } catch (err) {
    _iterator.e(err);
  } finally {
    _iterator.f();
  }

  return map;
}

export { createMapByKey };
