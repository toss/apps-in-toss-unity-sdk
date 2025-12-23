import { asyncToGenerator as _asyncToGenerator, regeneratorRuntime as _regeneratorRuntime } from './_virtual/_rollupPluginBabelHelpers.mjs';
import { copyToClipboard } from './copyToClipboard.mjs';
import { isIE } from './device/isIE.mjs';

function writeText(_x) {
  return _writeText.apply(this, arguments);
}

function _writeText() {
  _writeText = _asyncToGenerator( /*#__PURE__*/_regeneratorRuntime().mark(function _callee(text) {
    return _regeneratorRuntime().wrap(function _callee$(_context) {
      while (1) {
        switch (_context.prev = _context.next) {
          case 0:
            if (!(isIE() || !clipboardSupported())) {
              _context.next = 2;
              break;
            }

            return _context.abrupt("return", copyToClipboard(text));

          case 2:
            _context.prev = 2;
            _context.next = 5;
            return navigator.clipboard.writeText(text);

          case 5:
            return _context.abrupt("return", true);

          case 8:
            _context.prev = 8;
            _context.t0 = _context["catch"](2);
            return _context.abrupt("return", copyToClipboard(text));

          case 11:
          case "end":
            return _context.stop();
        }
      }
    }, _callee, null, [[2, 8]]);
  }));
  return _writeText.apply(this, arguments);
}

function clipboardSupported() {
  return navigator.clipboard != null;
}

var clipboard = {
  writeText: writeText
};

export { clipboard };
