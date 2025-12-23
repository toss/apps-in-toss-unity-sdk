import { asyncToGenerator as _asyncToGenerator, regeneratorRuntime as _regeneratorRuntime, asyncIterator as _asyncIterator, wrapAsyncGenerator as _wrapAsyncGenerator, awaitAsyncGenerator as _awaitAsyncGenerator, toConsumableArray as _toConsumableArray } from './_virtual/_rollupPluginBabelHelpers.mjs';

function retryRequestsOf(task, _ref) {
  var retries = _ref.retries,
      _ref$shouldRetry = _ref.shouldRetry,
      shouldRetry = _ref$shouldRetry === void 0 ? function () {
    return true;
  } : _ref$shouldRetry,
      onError = _ref.onError;
  return /*#__PURE__*/_asyncToGenerator( /*#__PURE__*/_regeneratorRuntime().mark(function _callee() {
    var retriesLeft,
        _iteratorAbruptCompletion,
        _didIteratorError,
        _iteratorError,
        _len,
        args,
        _key,
        _iterator,
        _step,
        result,
        _args = arguments;

    return _regeneratorRuntime().wrap(function _callee$(_context) {
      while (1) {
        switch (_context.prev = _context.next) {
          case 0:
            retriesLeft = retries;
            _iteratorAbruptCompletion = false;
            _didIteratorError = false;
            _context.prev = 3;

            for (_len = _args.length, args = new Array(_len), _key = 0; _key < _len; _key++) {
              args[_key] = _args[_key];
            }

            _iterator = _asyncIterator(createTaskStream(task, args));

          case 6:
            _context.next = 8;
            return _iterator.next();

          case 8:
            if (!(_iteratorAbruptCompletion = !(_step = _context.sent).done)) {
              _context.next = 25;
              break;
            }

            result = _step.value;
            _context.t0 = result.type;
            _context.next = _context.t0 === 'SUCCEEDED' ? 13 : _context.t0 === 'ERROR' ? 14 : 22;
            break;

          case 13:
            return _context.abrupt("return", result.value);

          case 14:
            _context.next = 16;
            return onError === null || onError === void 0 ? void 0 : onError(result.error);

          case 16:
            if (!(shouldRetry(result.error) && retriesLeft > 0)) {
              _context.next = 21;
              break;
            }

            retriesLeft -= 1;
            return _context.abrupt("continue", 22);

          case 21:
            throw result.error;

          case 22:
            _iteratorAbruptCompletion = false;
            _context.next = 6;
            break;

          case 25:
            _context.next = 31;
            break;

          case 27:
            _context.prev = 27;
            _context.t1 = _context["catch"](3);
            _didIteratorError = true;
            _iteratorError = _context.t1;

          case 31:
            _context.prev = 31;
            _context.prev = 32;

            if (!(_iteratorAbruptCompletion && _iterator["return"] != null)) {
              _context.next = 36;
              break;
            }

            _context.next = 36;
            return _iterator["return"]();

          case 36:
            _context.prev = 36;

            if (!_didIteratorError) {
              _context.next = 39;
              break;
            }

            throw _iteratorError;

          case 39:
            return _context.finish(36);

          case 40:
            return _context.finish(31);

          case 41:
            throw new Error('재시도 횟수를 초과했습니다.');

          case 42:
          case "end":
            return _context.stop();
        }
      }
    }, _callee, null, [[3, 27, 31, 41], [32,, 36, 40]]);
  }));
}

function createTaskStream(_x, _x2) {
  return _createTaskStream.apply(this, arguments);
}

function _createTaskStream() {
  _createTaskStream = _wrapAsyncGenerator( /*#__PURE__*/_regeneratorRuntime().mark(function _callee2(task, args) {
    var value;
    return _regeneratorRuntime().wrap(function _callee2$(_context2) {
      while (1) {
        switch (_context2.prev = _context2.next) {
          case 0:

            _context2.prev = 1;
            _context2.next = 4;
            return _awaitAsyncGenerator(task.apply(void 0, _toConsumableArray(args)));

          case 4:
            value = _context2.sent;
            _context2.next = 7;
            return {
              type: 'SUCCEEDED',
              value: value
            };

          case 7:
            _context2.next = 13;
            break;

          case 9:
            _context2.prev = 9;
            _context2.t0 = _context2["catch"](1);
            _context2.next = 13;
            return {
              type: 'ERROR',
              error: _context2.t0
            };

          case 13:
            _context2.next = 0;
            break;

          case 15:
          case "end":
            return _context2.stop();
        }
      }
    }, _callee2, null, [[1, 9]]);
  }));
  return _createTaskStream.apply(this, arguments);
}

export { retryRequestsOf };
