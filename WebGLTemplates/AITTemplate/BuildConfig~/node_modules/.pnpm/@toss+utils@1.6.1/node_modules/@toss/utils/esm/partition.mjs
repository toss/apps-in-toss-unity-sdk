import { createForOfIteratorHelper as _createForOfIteratorHelper } from './_virtual/_rollupPluginBabelHelpers.mjs';

/** @tossdocs-ignore */

/**
 * @deprecated This feature is now available in the es-toolkit package.
 */
function partition(items, predicate) {
  var first = [];
  var second = [];

  var _iterator = _createForOfIteratorHelper(items),
      _step;

  try {
    for (_iterator.s(); !(_step = _iterator.n()).done;) {
      var _item = _step.value;

      if (predicate(_item)) {
        first.push(_item);
      } else {
        second.push(_item);
      }
    }
  } catch (err) {
    _iterator.e(err);
  } finally {
    _iterator.f();
  }

  return [first, second];
}

export { partition };
