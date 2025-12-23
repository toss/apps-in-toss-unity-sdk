import { asyncToGenerator as _asyncToGenerator, regeneratorRuntime as _regeneratorRuntime } from './_virtual/_rollupPluginBabelHelpers.mjs';

/** @tossdocs-ignore */

/**
 * @deprecated This feature is now available in the es-toolkit package.
 */
var noop = function noop() {};
/**
 * @deprecated This feature is now available in the es-toolkit package.
 */

var asyncNoop = /*#__PURE__*/function () {
  var _ref = _asyncToGenerator( /*#__PURE__*/_regeneratorRuntime().mark(function _callee() {
    return _regeneratorRuntime().wrap(function _callee$(_context) {
      while (1) {
        switch (_context.prev = _context.next) {
          case 0:
          case "end":
            return _context.stop();
        }
      }
    }, _callee);
  }));

  return function asyncNoop() {
    return _ref.apply(this, arguments);
  };
}();

export { asyncNoop, noop };
