import { asyncToGenerator as _asyncToGenerator, regeneratorRuntime as _regeneratorRuntime } from './_virtual/_rollupPluginBabelHelpers.mjs';

/** @tossdocs-ignore */
function setFocusTimeout(_x, _x2) {
  return _setFocusTimeout.apply(this, arguments);
}

function _setFocusTimeout() {
  _setFocusTimeout = _asyncToGenerator( /*#__PURE__*/_regeneratorRuntime().mark(function _callee(executeFocus, delay) {
    return _regeneratorRuntime().wrap(function _callee$(_context) {
      while (1) {
        switch (_context.prev = _context.next) {
          case 0:
            return _context.abrupt("return", new Promise(function (resolve) {
              // It is impossible to focus asynchronously when it is not in focus,
              // but it is possible to move focus while it is in focus. This is a hack that uses this.
              // Create a fakeInput to focus and make it invisible.
              var fakeInput = document.createElement('input');
              fakeInput.setAttribute('type', 'text');
              fakeInput.style.position = 'absolute';
              fakeInput.style.opacity = '0';
              fakeInput.style.height = '0';
              fakeInput.style.fontSize = '16px'; // To disable auto zoom.

              fakeInput.readOnly = true; // Prevents the keyboard from coming up before actually focusing the input.

              document.body.prepend(fakeInput);
              fakeInput.focus({
                preventScroll: true // Prevents scrolling to the top.

              });
              setTimeout(function () {
                executeFocus();
                fakeInput.remove();
                resolve(true);
              }, delay);
            }));

          case 1:
          case "end":
            return _context.stop();
        }
      }
    }, _callee);
  }));
  return _setFocusTimeout.apply(this, arguments);
}

export { setFocusTimeout };
