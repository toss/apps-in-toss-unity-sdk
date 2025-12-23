import { asyncToGenerator as _asyncToGenerator, regeneratorRuntime as _regeneratorRuntime } from './_virtual/_rollupPluginBabelHelpers.mjs';

/** @tossdocs-ignore */
function safeScrollTo(element, options) {
  if (!element) {
    return;
  }

  if (isWindow(element)) {
    element.scrollTo(options.left !== undefined ? options.left : element.scrollX, options.top !== undefined ? options.top : element.scrollY);
  } else if (element.scrollTo) {
    element.scrollTo(options);
  } else {
    var left = options.left,
        top = options.top;

    if (top !== undefined) {
      element.scrollTop = top;
    }

    if (left !== undefined) {
      element.scrollLeft = left;
    }
  }
}
function safeSmoothScrollTo(element, _ref) {
  var scrollTargetY = _ref.top;
  var speed = arguments.length > 2 && arguments[2] !== undefined ? arguments[2] : 2000;
  smoothScrollTo(element, {
    top: scrollTargetY
  }, {
    speed: speed
  });
}
function smoothScrollTo(_x, _x2) {
  return _smoothScrollTo.apply(this, arguments);
}

function _smoothScrollTo() {
  _smoothScrollTo = _asyncToGenerator( /*#__PURE__*/_regeneratorRuntime().mark(function _callee(element, scrollTo) {
    var config,
        _args = arguments;
    return _regeneratorRuntime().wrap(function _callee$(_context) {
      while (1) {
        switch (_context.prev = _context.next) {
          case 0:
            config = _args.length > 2 && _args[2] !== undefined ? _args[2] : {
              speed: 2000
            };
            return _context.abrupt("return", new Promise(function (res) {
              var scrollY = isWindow(element) ? element.scrollY : element.scrollTop;
              var totalAnimatingTime = 'duration' in config ? config.duration / 1000 : Math.max(0.1, Math.min(Math.abs(scrollY - scrollTo.top) / config.speed, 0.8));

              function ease(animationProgress) {
                return -0.5 * (Math.cos(Math.PI * animationProgress) - 1);
              }

              function tick(currentTime) {
                var progress = currentTime / totalAnimatingTime;
                var easedProgress = ease(progress);

                if (progress < 1) {
                  window.requestAnimationFrame(function () {
                    return tick(currentTime + 1 / 60);
                  });
                  safeScrollTo(element, {
                    top: scrollY + (scrollTo.top - scrollY) * easedProgress
                  });
                } else {
                  safeScrollTo(element, scrollTo);
                  res();
                }
              }

              tick(0);
            }));

          case 2:
          case "end":
            return _context.stop();
        }
      }
    }, _callee);
  }));
  return _smoothScrollTo.apply(this, arguments);
}

function isWindow(el) {
  return el === window;
}

export { safeScrollTo, safeSmoothScrollTo, smoothScrollTo };
