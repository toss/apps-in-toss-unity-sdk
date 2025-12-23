import { toConsumableArray as _toConsumableArray } from './_virtual/_rollupPluginBabelHelpers.mjs';

/** @tossdocs-ignore */

/**
 * @deprecated This feature is now available in the es-toolkit package.
 */
function zip() {
  for (var _len = arguments.length, arrays = new Array(_len), _key = 0; _key < _len; _key++) {
    arrays[_key] = arguments[_key];
  }

  var length = Math.max.apply(Math, _toConsumableArray(arrays.map(function (x) {
    return x.length;
  })));
  var result = [];

  var _loop = function _loop(index) {
    result.push(arrays.map(function (x) {
      return x[index];
    }));
  };

  for (var index = 0; index < length; index++) {
    _loop(index);
  }

  return result;
}

export { zip };
