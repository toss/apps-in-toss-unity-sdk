import { noop } from './noop.mjs';

/** @tossdocs-ignore */
function batchRequestsOf(func) {
  var promiseByKey = new Map();
  return function () {
    for (var _len = arguments.length, args = new Array(_len), _key = 0; _key < _len; _key++) {
      args[_key] = arguments[_key];
    }

    var key = JSON.stringify(args);

    if (promiseByKey.has(key)) {
      return promiseByKey.get(key);
    } else {
      var promise = func.apply(void 0, args);
      promise.then(function () {
        promiseByKey["delete"](key);
      }, noop);
      promiseByKey.set(key, promise);
      return promise;
    }
  };
}

export { batchRequestsOf };
