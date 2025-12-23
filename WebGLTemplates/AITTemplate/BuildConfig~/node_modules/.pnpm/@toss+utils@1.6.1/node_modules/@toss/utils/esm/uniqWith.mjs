import { createForOfIteratorHelper as _createForOfIteratorHelper } from './_virtual/_rollupPluginBabelHelpers.mjs';

/** @tossdocs-ignore */

/**
 * @deprecated This feature is now available in the es-toolkit package.
 */
function uniqWith(arr, comparator) {
  var result = [];

  var _iterator = _createForOfIteratorHelper(arr),
      _step;

  try {
    var _loop = function _loop() {
      var item = _step.value;

      if (result.some(function (x) {
        return comparator(x, item);
      })) {
        return "continue";
      }

      result.push(item);
    };

    for (_iterator.s(); !(_step = _iterator.n()).done;) {
      var _ret = _loop();

      if (_ret === "continue") continue;
    }
  } catch (err) {
    _iterator.e(err);
  } finally {
    _iterator.f();
  }

  return result;
}

export { uniqWith };
