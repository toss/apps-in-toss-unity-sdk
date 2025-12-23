'use strict';

Object.defineProperty(exports, '__esModule', { value: true });

var dateFns = require('date-fns');

/** @tossdocs-ignore */
function isServer() {
  return typeof window === 'undefined' || 'Deno' in globalThis;
}

/** @tossdocs-ignore */
function isAndroid() {
  if (isServer()) {
    return false;
  }

  return navigator.userAgent.match(/Android/i) !== null;
}

/** @tossdocs-ignore */
function isIOS() {
  if (isServer()) {
    return false;
  }

  return navigator.userAgent.match(/ipad|iphone/i) !== null;
}

/** @tossdocs-ignore */
function getOSByUserAgent() {
  if (isServer()) {
    return false;
  }

  if (isIOS()) {
    return 'ios';
  }

  if (isAndroid()) {
    return 'android';
  }

  return 'web';
}

/** @tossdocs-ignore */
function isClient() {
  return !isServer();
}

/** @tossdocs-ignore */
function isIE() {
  if (isServer()) {
    return false;
  }

  return /MSIE|Trident/i.test(window.navigator.userAgent);
}

/** @tossdocs-ignore */
function isMacOS() {
  if (isServer()) {
    return false;
  }

  return navigator.platform.match(/Macintosh|MacIntel|MacPPC|Mac68K/) !== null;
}

/** @tossdocs-ignore */
function isMobileWeb() {
  var userAgent = getOSByUserAgent();

  if (userAgent === 'ios' || userAgent === 'android') {
    return true;
  }

  return false;
}

/** @tossdocs-ignore */

function loadScript(source) {
  if (isServer()) {
    return Promise.resolve();
  }

  var element = document.querySelector("script[src=\"".concat(source, "\"]")); // 이미 로드되어 있거나 로드 중

  if (element) {
    return Promise.resolve();
  }

  return new Promise(function (resolve) {
    var script = document.createElement('script');
    script.async = true;
    script.defer = true;
    script.src = source;
    document.body.append(script);
    script.addEventListener('load', resolve);
  });
}

/** @tossdocs-ignore */
function maskAll(str) {
  return str.replace(/./g, '*');
}

function isKoreanName(name) {
  return /[가-힣]{2,}/.test(name);
}

function maskName$1(name) {
  if (isKoreanName(name)) {
    switch (name.length) {
      case 2:
        return name.replace(/([가-힣])([가-힣]+)/, '$1*');

      default:
        return maskExceptForEdge(name, 1);
    }
  } else {
    if (name.length < 3) {
      return name;
    }

    var unmaskedSideSize = name.length < 6 ? 1 : 2;
    return maskExceptForEdge(name, unmaskedSideSize);
  }
}

function maskExceptForEdge(text, edgeSize) {
  return text.slice(0, edgeSize) + text.slice(edgeSize, text.length - edgeSize).replace(/[a-zA-Z가-힣]/g, '*') + text.slice(text.length - edgeSize, text.length);
}

function isHyphenSeparated(phoneNumber) {
  return /^\d{2,3}-\d{3,4}-\d{4}$/.test(phoneNumber);
}

function isSeoulPhoneNumber(phoneNumber) {
  return /^02\d+$/.test(phoneNumber);
}

function maskPhoneNumber(phoneNumber) {
  if (isHyphenSeparated(phoneNumber)) {
    return phoneNumber.replace(/^(\d{2,3})-(\d{3,4})-(\d{4})$/, function (_, p1, p2, p3) {
      return "".concat(p1, "-").concat(maskAll(p2), "-").concat(p3);
    });
  }

  if (isSeoulPhoneNumber(phoneNumber)) {
    return phoneNumber.replace(/^02(\d{3,4})(\d{4})/, function (_, p1, p2) {
      return "02".concat(maskAll(p1)).concat(p2);
    });
  }

  return phoneNumber.replace(/^(\d{3})(\d{3,4})(\d{4})/, function (_, p1, p2, p3) {
    return "".concat(p1).concat(maskAll(p2)).concat(p3);
  });
}

var Masker = {
  maskName: maskName$1,
  maskPhoneNumber: maskPhoneNumber
};

/** @tossdocs-ignore */
var units = ['', '십', '백', '천', '만', '십', '백', '천', '억', '십', '백', '천', '조', '십', '백', '천', '경'];

function chunk$1(value, byDigits) {
  var result = [];
  var source = String(value);

  for (var end = source.length; end >= 1; end = end - byDigits) {
    var start = Math.max(end - byDigits, 0);
    var slice = source.slice(start, end);
    result.push(Number(slice));
  }

  return result;
}

function createNumberFormatterBy(formatter) {
  return function formatNumber(value, unit) {
    if (unit < 1) {
      // 부동소수점 오류 때문에 unit < 1인 경우 특별 처리
      var reciprocal = 1 / unit;
      return formatter(value * reciprocal) / reciprocal;
    }

    return formatter(value / unit) * unit;
  };
}

var ceilToUnit = createNumberFormatterBy(Math.ceil);
var floorToUnit = createNumberFormatterBy(Math.floor);
var roundToUnit = createNumberFormatterBy(Math.round);
function formatToKoreanNumber(value) {
  var options = arguments.length > 1 && arguments[1] !== undefined ? arguments[1] : {};
  var unit = options.floorUnit !== undefined ? floorToUnit(value, options.floorUnit || 1) : ceilToUnit(value, options.ceilUnit || 1);

  if (unit === 0) {
    return '0';
  }

  return chunk$1(unit, 4).reduce(function (prevFormatted, currChunkNum, index) {
    if (currChunkNum === 0) {
      return prevFormatted;
    }

    var val = options.formatAllDigits ? formatThousands(currChunkNum) : commaizeNumber(currChunkNum);
    var unit = units[index * 4];
    return "".concat(val).concat(unit, " ").concat(prevFormatted);
  }, '').trim();
}
function formatToKRW(value) {
  var options = arguments.length > 1 && arguments[1] !== undefined ? arguments[1] : {};
  var formattedVal = formatToKoreanNumber(value, options);

  if (options.shouldHaveSpaceBeforeWon === true) {
    return "".concat(formattedVal, " \uC6D0");
  }

  return "".concat(formattedVal, "\uC6D0");
}
function commaizeNumber(value) {
  var numStr = String(value);
  var decimalPointIndex = numStr.indexOf('.');
  var commaizeRegExp = /(\d)(?=(\d\d\d)+(?!\d))/g;
  return decimalPointIndex > -1 ? numStr.slice(0, decimalPointIndex).replace(commaizeRegExp, '$1,') + numStr.slice(decimalPointIndex) : numStr.replace(commaizeRegExp, '$1,');
}
function floorAndFormatNumber(value) {
  return commaizeNumber(Math.floor(value));
}
function decommaizeNumber(numStr) {
  return Number(numStr.replace(/,/g, ''));
}
function formatPhoneNumber(phoneNumber) {
  // 서울 국번(02)인 경우에만 지역번호가 2자리입니다.
  var isSeoulNumber = phoneNumber.startsWith('02'); // 12자리 전화번호는 앞자리가 4개입니다.

  var is12Number = phoneNumber.length === 12;
  var areaCodeEndIndex = isSeoulNumber ? 2 : is12Number ? 4 : 3; // 9 ~ 12자리 전화번호에 대응하기 위해서
  // [0:areaCodeEndIndex], [areaCodeEndIndex:length-4], [length-4:length] 형식으로 나누고 join합니다.

  return [phoneNumber.slice(0, areaCodeEndIndex), phoneNumber.slice(areaCodeEndIndex, phoneNumber.length - 4), phoneNumber.slice(phoneNumber.length - 4)].join('-');
}

function formatThousands(num) {
  var numString = String(num).split('').reverse().map(function (digit, index) {
    return digit !== '0' ? "".concat(digit !== '1' ? digit : '').concat(units[index]) : '';
  }).reverse().join('');
  return numString;
}

function formatBusinessRegistrationNumber(businessRegistrationNumber) {
  if (businessRegistrationNumber.length !== 10) {
    throw new Error('사업자등록번호는 반드시 길이가 10 이어야 합니다.');
  }

  if (/^\d+$/.test(businessRegistrationNumber) === false) {
    throw new Error('사업자등록번호는 [0-9] 이어야 합니다.');
  }

  return businessRegistrationNumber.replace(/(\d{3})(\d{2})(\d{5})/, '$1-$2-$3');
}

/** @tossdocs-ignore */
function arrayIncludes(array, item, fromIndex) {
  return array.includes(item, fromIndex);
}

/** @tossdocs-ignore */
function isDifferentArray() {
  var a = arguments.length > 0 && arguments[0] !== undefined ? arguments[0] : [];
  var b = arguments.length > 1 && arguments[1] !== undefined ? arguments[1] : [];
  return a.length !== b.length || a.some(function (item, index) {
    return !Object.is(item, b[index]);
  });
}

/** @tossdocs-ignore */
function isNonEmptyArray(array) {
  return array.length >= 1;
}

/** @tossdocs-ignore */

/**
 * @deprecated This feature is now available in the es-toolkit package.
 */

/**
 * @deprecated This feature is now available in the es-toolkit package.
 */

/**
 * @deprecated This feature is now available in the es-toolkit package.
 */
function last(arr) {
  return arr[arr.length - 1];
}

/** @tossdocs-ignore */

/**
 * @deprecated This feature is now available in the es-toolkit package.
 */

/**
 * @deprecated This feature is now available in the es-toolkit package.
 */

/**
 * @deprecated This feature is now available in the es-toolkit package.
 */
function sample(arr) {
  var randomIndex = Math.floor(arr.length * Math.random());
  return arr[randomIndex];
}

function _AsyncGenerator(gen) {
  var front, back;

  function resume(key, arg) {
    try {
      var result = gen[key](arg),
          value = result.value,
          overloaded = value instanceof _OverloadYield;
      Promise.resolve(overloaded ? value.v : value).then(function (arg) {
        if (overloaded) {
          var nextKey = "return" === key ? "return" : "next";
          if (!value.k || arg.done) return resume(nextKey, arg);
          arg = gen[nextKey](arg).value;
        }

        settle(result.done ? "return" : "normal", arg);
      }, function (err) {
        resume("throw", err);
      });
    } catch (err) {
      settle("throw", err);
    }
  }

  function settle(type, value) {
    switch (type) {
      case "return":
        front.resolve({
          value: value,
          done: !0
        });
        break;

      case "throw":
        front.reject(value);
        break;

      default:
        front.resolve({
          value: value,
          done: !1
        });
    }

    (front = front.next) ? resume(front.key, front.arg) : back = null;
  }

  this._invoke = function (key, arg) {
    return new Promise(function (resolve, reject) {
      var request = {
        key: key,
        arg: arg,
        resolve: resolve,
        reject: reject,
        next: null
      };
      back ? back = back.next = request : (front = back = request, resume(key, arg));
    });
  }, "function" != typeof gen.return && (this.return = void 0);
}

_AsyncGenerator.prototype["function" == typeof Symbol && Symbol.asyncIterator || "@@asyncIterator"] = function () {
  return this;
}, _AsyncGenerator.prototype.next = function (arg) {
  return this._invoke("next", arg);
}, _AsyncGenerator.prototype.throw = function (arg) {
  return this._invoke("throw", arg);
}, _AsyncGenerator.prototype.return = function (arg) {
  return this._invoke("return", arg);
};

function _OverloadYield(value, kind) {
  this.v = value, this.k = kind;
}

function _asyncIterator(iterable) {
  var method,
      async,
      sync,
      retry = 2;

  for ("undefined" != typeof Symbol && (async = Symbol.asyncIterator, sync = Symbol.iterator); retry--;) {
    if (async && null != (method = iterable[async])) return method.call(iterable);
    if (sync && null != (method = iterable[sync])) return new AsyncFromSyncIterator(method.call(iterable));
    async = "@@asyncIterator", sync = "@@iterator";
  }

  throw new TypeError("Object is not async iterable");
}

function AsyncFromSyncIterator(s) {
  function AsyncFromSyncIteratorContinuation(r) {
    if (Object(r) !== r) return Promise.reject(new TypeError(r + " is not an object."));
    var done = r.done;
    return Promise.resolve(r.value).then(function (value) {
      return {
        value: value,
        done: done
      };
    });
  }

  return AsyncFromSyncIterator = function (s) {
    this.s = s, this.n = s.next;
  }, AsyncFromSyncIterator.prototype = {
    s: null,
    n: null,
    next: function () {
      return AsyncFromSyncIteratorContinuation(this.n.apply(this.s, arguments));
    },
    return: function (value) {
      var ret = this.s.return;
      return void 0 === ret ? Promise.resolve({
        value: value,
        done: !0
      }) : AsyncFromSyncIteratorContinuation(ret.apply(this.s, arguments));
    },
    throw: function (value) {
      var thr = this.s.return;
      return void 0 === thr ? Promise.reject(value) : AsyncFromSyncIteratorContinuation(thr.apply(this.s, arguments));
    }
  }, new AsyncFromSyncIterator(s);
}

function _awaitAsyncGenerator(value) {
  return new _OverloadYield(value, 0);
}

function ownKeys(object, enumerableOnly) {
  var keys = Object.keys(object);

  if (Object.getOwnPropertySymbols) {
    var symbols = Object.getOwnPropertySymbols(object);
    enumerableOnly && (symbols = symbols.filter(function (sym) {
      return Object.getOwnPropertyDescriptor(object, sym).enumerable;
    })), keys.push.apply(keys, symbols);
  }

  return keys;
}

function _objectSpread2(target) {
  for (var i = 1; i < arguments.length; i++) {
    var source = null != arguments[i] ? arguments[i] : {};
    i % 2 ? ownKeys(Object(source), !0).forEach(function (key) {
      _defineProperty(target, key, source[key]);
    }) : Object.getOwnPropertyDescriptors ? Object.defineProperties(target, Object.getOwnPropertyDescriptors(source)) : ownKeys(Object(source)).forEach(function (key) {
      Object.defineProperty(target, key, Object.getOwnPropertyDescriptor(source, key));
    });
  }

  return target;
}

function _regeneratorRuntime() {
  /*! regenerator-runtime -- Copyright (c) 2014-present, Facebook, Inc. -- license (MIT): https://github.com/facebook/regenerator/blob/main/LICENSE */

  _regeneratorRuntime = function () {
    return exports;
  };

  var exports = {},
      Op = Object.prototype,
      hasOwn = Op.hasOwnProperty,
      $Symbol = "function" == typeof Symbol ? Symbol : {},
      iteratorSymbol = $Symbol.iterator || "@@iterator",
      asyncIteratorSymbol = $Symbol.asyncIterator || "@@asyncIterator",
      toStringTagSymbol = $Symbol.toStringTag || "@@toStringTag";

  function define(obj, key, value) {
    return Object.defineProperty(obj, key, {
      value: value,
      enumerable: !0,
      configurable: !0,
      writable: !0
    }), obj[key];
  }

  try {
    define({}, "");
  } catch (err) {
    define = function (obj, key, value) {
      return obj[key] = value;
    };
  }

  function wrap(innerFn, outerFn, self, tryLocsList) {
    var protoGenerator = outerFn && outerFn.prototype instanceof Generator ? outerFn : Generator,
        generator = Object.create(protoGenerator.prototype),
        context = new Context(tryLocsList || []);
    return generator._invoke = function (innerFn, self, context) {
      var state = "suspendedStart";
      return function (method, arg) {
        if ("executing" === state) throw new Error("Generator is already running");

        if ("completed" === state) {
          if ("throw" === method) throw arg;
          return doneResult();
        }

        for (context.method = method, context.arg = arg;;) {
          var delegate = context.delegate;

          if (delegate) {
            var delegateResult = maybeInvokeDelegate(delegate, context);

            if (delegateResult) {
              if (delegateResult === ContinueSentinel) continue;
              return delegateResult;
            }
          }

          if ("next" === context.method) context.sent = context._sent = context.arg;else if ("throw" === context.method) {
            if ("suspendedStart" === state) throw state = "completed", context.arg;
            context.dispatchException(context.arg);
          } else "return" === context.method && context.abrupt("return", context.arg);
          state = "executing";
          var record = tryCatch(innerFn, self, context);

          if ("normal" === record.type) {
            if (state = context.done ? "completed" : "suspendedYield", record.arg === ContinueSentinel) continue;
            return {
              value: record.arg,
              done: context.done
            };
          }

          "throw" === record.type && (state = "completed", context.method = "throw", context.arg = record.arg);
        }
      };
    }(innerFn, self, context), generator;
  }

  function tryCatch(fn, obj, arg) {
    try {
      return {
        type: "normal",
        arg: fn.call(obj, arg)
      };
    } catch (err) {
      return {
        type: "throw",
        arg: err
      };
    }
  }

  exports.wrap = wrap;
  var ContinueSentinel = {};

  function Generator() {}

  function GeneratorFunction() {}

  function GeneratorFunctionPrototype() {}

  var IteratorPrototype = {};
  define(IteratorPrototype, iteratorSymbol, function () {
    return this;
  });
  var getProto = Object.getPrototypeOf,
      NativeIteratorPrototype = getProto && getProto(getProto(values([])));
  NativeIteratorPrototype && NativeIteratorPrototype !== Op && hasOwn.call(NativeIteratorPrototype, iteratorSymbol) && (IteratorPrototype = NativeIteratorPrototype);
  var Gp = GeneratorFunctionPrototype.prototype = Generator.prototype = Object.create(IteratorPrototype);

  function defineIteratorMethods(prototype) {
    ["next", "throw", "return"].forEach(function (method) {
      define(prototype, method, function (arg) {
        return this._invoke(method, arg);
      });
    });
  }

  function AsyncIterator(generator, PromiseImpl) {
    function invoke(method, arg, resolve, reject) {
      var record = tryCatch(generator[method], generator, arg);

      if ("throw" !== record.type) {
        var result = record.arg,
            value = result.value;
        return value && "object" == typeof value && hasOwn.call(value, "__await") ? PromiseImpl.resolve(value.__await).then(function (value) {
          invoke("next", value, resolve, reject);
        }, function (err) {
          invoke("throw", err, resolve, reject);
        }) : PromiseImpl.resolve(value).then(function (unwrapped) {
          result.value = unwrapped, resolve(result);
        }, function (error) {
          return invoke("throw", error, resolve, reject);
        });
      }

      reject(record.arg);
    }

    var previousPromise;

    this._invoke = function (method, arg) {
      function callInvokeWithMethodAndArg() {
        return new PromiseImpl(function (resolve, reject) {
          invoke(method, arg, resolve, reject);
        });
      }

      return previousPromise = previousPromise ? previousPromise.then(callInvokeWithMethodAndArg, callInvokeWithMethodAndArg) : callInvokeWithMethodAndArg();
    };
  }

  function maybeInvokeDelegate(delegate, context) {
    var method = delegate.iterator[context.method];

    if (undefined === method) {
      if (context.delegate = null, "throw" === context.method) {
        if (delegate.iterator.return && (context.method = "return", context.arg = undefined, maybeInvokeDelegate(delegate, context), "throw" === context.method)) return ContinueSentinel;
        context.method = "throw", context.arg = new TypeError("The iterator does not provide a 'throw' method");
      }

      return ContinueSentinel;
    }

    var record = tryCatch(method, delegate.iterator, context.arg);
    if ("throw" === record.type) return context.method = "throw", context.arg = record.arg, context.delegate = null, ContinueSentinel;
    var info = record.arg;
    return info ? info.done ? (context[delegate.resultName] = info.value, context.next = delegate.nextLoc, "return" !== context.method && (context.method = "next", context.arg = undefined), context.delegate = null, ContinueSentinel) : info : (context.method = "throw", context.arg = new TypeError("iterator result is not an object"), context.delegate = null, ContinueSentinel);
  }

  function pushTryEntry(locs) {
    var entry = {
      tryLoc: locs[0]
    };
    1 in locs && (entry.catchLoc = locs[1]), 2 in locs && (entry.finallyLoc = locs[2], entry.afterLoc = locs[3]), this.tryEntries.push(entry);
  }

  function resetTryEntry(entry) {
    var record = entry.completion || {};
    record.type = "normal", delete record.arg, entry.completion = record;
  }

  function Context(tryLocsList) {
    this.tryEntries = [{
      tryLoc: "root"
    }], tryLocsList.forEach(pushTryEntry, this), this.reset(!0);
  }

  function values(iterable) {
    if (iterable) {
      var iteratorMethod = iterable[iteratorSymbol];
      if (iteratorMethod) return iteratorMethod.call(iterable);
      if ("function" == typeof iterable.next) return iterable;

      if (!isNaN(iterable.length)) {
        var i = -1,
            next = function next() {
          for (; ++i < iterable.length;) if (hasOwn.call(iterable, i)) return next.value = iterable[i], next.done = !1, next;

          return next.value = undefined, next.done = !0, next;
        };

        return next.next = next;
      }
    }

    return {
      next: doneResult
    };
  }

  function doneResult() {
    return {
      value: undefined,
      done: !0
    };
  }

  return GeneratorFunction.prototype = GeneratorFunctionPrototype, define(Gp, "constructor", GeneratorFunctionPrototype), define(GeneratorFunctionPrototype, "constructor", GeneratorFunction), GeneratorFunction.displayName = define(GeneratorFunctionPrototype, toStringTagSymbol, "GeneratorFunction"), exports.isGeneratorFunction = function (genFun) {
    var ctor = "function" == typeof genFun && genFun.constructor;
    return !!ctor && (ctor === GeneratorFunction || "GeneratorFunction" === (ctor.displayName || ctor.name));
  }, exports.mark = function (genFun) {
    return Object.setPrototypeOf ? Object.setPrototypeOf(genFun, GeneratorFunctionPrototype) : (genFun.__proto__ = GeneratorFunctionPrototype, define(genFun, toStringTagSymbol, "GeneratorFunction")), genFun.prototype = Object.create(Gp), genFun;
  }, exports.awrap = function (arg) {
    return {
      __await: arg
    };
  }, defineIteratorMethods(AsyncIterator.prototype), define(AsyncIterator.prototype, asyncIteratorSymbol, function () {
    return this;
  }), exports.AsyncIterator = AsyncIterator, exports.async = function (innerFn, outerFn, self, tryLocsList, PromiseImpl) {
    void 0 === PromiseImpl && (PromiseImpl = Promise);
    var iter = new AsyncIterator(wrap(innerFn, outerFn, self, tryLocsList), PromiseImpl);
    return exports.isGeneratorFunction(outerFn) ? iter : iter.next().then(function (result) {
      return result.done ? result.value : iter.next();
    });
  }, defineIteratorMethods(Gp), define(Gp, toStringTagSymbol, "Generator"), define(Gp, iteratorSymbol, function () {
    return this;
  }), define(Gp, "toString", function () {
    return "[object Generator]";
  }), exports.keys = function (object) {
    var keys = [];

    for (var key in object) keys.push(key);

    return keys.reverse(), function next() {
      for (; keys.length;) {
        var key = keys.pop();
        if (key in object) return next.value = key, next.done = !1, next;
      }

      return next.done = !0, next;
    };
  }, exports.values = values, Context.prototype = {
    constructor: Context,
    reset: function (skipTempReset) {
      if (this.prev = 0, this.next = 0, this.sent = this._sent = undefined, this.done = !1, this.delegate = null, this.method = "next", this.arg = undefined, this.tryEntries.forEach(resetTryEntry), !skipTempReset) for (var name in this) "t" === name.charAt(0) && hasOwn.call(this, name) && !isNaN(+name.slice(1)) && (this[name] = undefined);
    },
    stop: function () {
      this.done = !0;
      var rootRecord = this.tryEntries[0].completion;
      if ("throw" === rootRecord.type) throw rootRecord.arg;
      return this.rval;
    },
    dispatchException: function (exception) {
      if (this.done) throw exception;
      var context = this;

      function handle(loc, caught) {
        return record.type = "throw", record.arg = exception, context.next = loc, caught && (context.method = "next", context.arg = undefined), !!caught;
      }

      for (var i = this.tryEntries.length - 1; i >= 0; --i) {
        var entry = this.tryEntries[i],
            record = entry.completion;
        if ("root" === entry.tryLoc) return handle("end");

        if (entry.tryLoc <= this.prev) {
          var hasCatch = hasOwn.call(entry, "catchLoc"),
              hasFinally = hasOwn.call(entry, "finallyLoc");

          if (hasCatch && hasFinally) {
            if (this.prev < entry.catchLoc) return handle(entry.catchLoc, !0);
            if (this.prev < entry.finallyLoc) return handle(entry.finallyLoc);
          } else if (hasCatch) {
            if (this.prev < entry.catchLoc) return handle(entry.catchLoc, !0);
          } else {
            if (!hasFinally) throw new Error("try statement without catch or finally");
            if (this.prev < entry.finallyLoc) return handle(entry.finallyLoc);
          }
        }
      }
    },
    abrupt: function (type, arg) {
      for (var i = this.tryEntries.length - 1; i >= 0; --i) {
        var entry = this.tryEntries[i];

        if (entry.tryLoc <= this.prev && hasOwn.call(entry, "finallyLoc") && this.prev < entry.finallyLoc) {
          var finallyEntry = entry;
          break;
        }
      }

      finallyEntry && ("break" === type || "continue" === type) && finallyEntry.tryLoc <= arg && arg <= finallyEntry.finallyLoc && (finallyEntry = null);
      var record = finallyEntry ? finallyEntry.completion : {};
      return record.type = type, record.arg = arg, finallyEntry ? (this.method = "next", this.next = finallyEntry.finallyLoc, ContinueSentinel) : this.complete(record);
    },
    complete: function (record, afterLoc) {
      if ("throw" === record.type) throw record.arg;
      return "break" === record.type || "continue" === record.type ? this.next = record.arg : "return" === record.type ? (this.rval = this.arg = record.arg, this.method = "return", this.next = "end") : "normal" === record.type && afterLoc && (this.next = afterLoc), ContinueSentinel;
    },
    finish: function (finallyLoc) {
      for (var i = this.tryEntries.length - 1; i >= 0; --i) {
        var entry = this.tryEntries[i];
        if (entry.finallyLoc === finallyLoc) return this.complete(entry.completion, entry.afterLoc), resetTryEntry(entry), ContinueSentinel;
      }
    },
    catch: function (tryLoc) {
      for (var i = this.tryEntries.length - 1; i >= 0; --i) {
        var entry = this.tryEntries[i];

        if (entry.tryLoc === tryLoc) {
          var record = entry.completion;

          if ("throw" === record.type) {
            var thrown = record.arg;
            resetTryEntry(entry);
          }

          return thrown;
        }
      }

      throw new Error("illegal catch attempt");
    },
    delegateYield: function (iterable, resultName, nextLoc) {
      return this.delegate = {
        iterator: values(iterable),
        resultName: resultName,
        nextLoc: nextLoc
      }, "next" === this.method && (this.arg = undefined), ContinueSentinel;
    }
  }, exports;
}

function _wrapAsyncGenerator(fn) {
  return function () {
    return new _AsyncGenerator(fn.apply(this, arguments));
  };
}

function asyncGeneratorStep(gen, resolve, reject, _next, _throw, key, arg) {
  try {
    var info = gen[key](arg);
    var value = info.value;
  } catch (error) {
    reject(error);
    return;
  }

  if (info.done) {
    resolve(value);
  } else {
    Promise.resolve(value).then(_next, _throw);
  }
}

function _asyncToGenerator(fn) {
  return function () {
    var self = this,
        args = arguments;
    return new Promise(function (resolve, reject) {
      var gen = fn.apply(self, args);

      function _next(value) {
        asyncGeneratorStep(gen, resolve, reject, _next, _throw, "next", value);
      }

      function _throw(err) {
        asyncGeneratorStep(gen, resolve, reject, _next, _throw, "throw", err);
      }

      _next(undefined);
    });
  };
}

function _defineProperty(obj, key, value) {
  if (key in obj) {
    Object.defineProperty(obj, key, {
      value: value,
      enumerable: true,
      configurable: true,
      writable: true
    });
  } else {
    obj[key] = value;
  }

  return obj;
}

function _slicedToArray(arr, i) {
  return _arrayWithHoles(arr) || _iterableToArrayLimit(arr, i) || _unsupportedIterableToArray(arr, i) || _nonIterableRest();
}

function _toConsumableArray(arr) {
  return _arrayWithoutHoles(arr) || _iterableToArray(arr) || _unsupportedIterableToArray(arr) || _nonIterableSpread();
}

function _arrayWithoutHoles(arr) {
  if (Array.isArray(arr)) return _arrayLikeToArray(arr);
}

function _arrayWithHoles(arr) {
  if (Array.isArray(arr)) return arr;
}

function _iterableToArray(iter) {
  if (typeof Symbol !== "undefined" && iter[Symbol.iterator] != null || iter["@@iterator"] != null) return Array.from(iter);
}

function _iterableToArrayLimit(arr, i) {
  var _i = arr == null ? null : typeof Symbol !== "undefined" && arr[Symbol.iterator] || arr["@@iterator"];

  if (_i == null) return;
  var _arr = [];
  var _n = true;
  var _d = false;

  var _s, _e;

  try {
    for (_i = _i.call(arr); !(_n = (_s = _i.next()).done); _n = true) {
      _arr.push(_s.value);

      if (i && _arr.length === i) break;
    }
  } catch (err) {
    _d = true;
    _e = err;
  } finally {
    try {
      if (!_n && _i["return"] != null) _i["return"]();
    } finally {
      if (_d) throw _e;
    }
  }

  return _arr;
}

function _unsupportedIterableToArray(o, minLen) {
  if (!o) return;
  if (typeof o === "string") return _arrayLikeToArray(o, minLen);
  var n = Object.prototype.toString.call(o).slice(8, -1);
  if (n === "Object" && o.constructor) n = o.constructor.name;
  if (n === "Map" || n === "Set") return Array.from(o);
  if (n === "Arguments" || /^(?:Ui|I)nt(?:8|16|32)(?:Clamped)?Array$/.test(n)) return _arrayLikeToArray(o, minLen);
}

function _arrayLikeToArray(arr, len) {
  if (len == null || len > arr.length) len = arr.length;

  for (var i = 0, arr2 = new Array(len); i < len; i++) arr2[i] = arr[i];

  return arr2;
}

function _nonIterableSpread() {
  throw new TypeError("Invalid attempt to spread non-iterable instance.\nIn order to be iterable, non-array objects must have a [Symbol.iterator]() method.");
}

function _nonIterableRest() {
  throw new TypeError("Invalid attempt to destructure non-iterable instance.\nIn order to be iterable, non-array objects must have a [Symbol.iterator]() method.");
}

function _createForOfIteratorHelper(o, allowArrayLike) {
  var it = typeof Symbol !== "undefined" && o[Symbol.iterator] || o["@@iterator"];

  if (!it) {
    if (Array.isArray(o) || (it = _unsupportedIterableToArray(o)) || allowArrayLike && o && typeof o.length === "number") {
      if (it) o = it;
      var i = 0;

      var F = function () {};

      return {
        s: F,
        n: function () {
          if (i >= o.length) return {
            done: true
          };
          return {
            done: false,
            value: o[i++]
          };
        },
        e: function (e) {
          throw e;
        },
        f: F
      };
    }

    throw new TypeError("Invalid attempt to iterate non-iterable instance.\nIn order to be iterable, non-array objects must have a [Symbol.iterator]() method.");
  }

  var normalCompletion = true,
      didErr = false,
      err;
  return {
    s: function () {
      it = it.call(o);
    },
    n: function () {
      var step = it.next();
      normalCompletion = step.done;
      return step;
    },
    e: function (e) {
      didErr = true;
      err = e;
    },
    f: function () {
      try {
        if (!normalCompletion && it.return != null) it.return();
      } finally {
        if (didErr) throw err;
      }
    }
  };
}

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

/** @tossdocs-ignore */

/**
 * @deprecated This feature is now available in the es-toolkit package.
 */
function chunk(arr, size) {
  if (size < 1) {
    return [];
  }

  return arr.reduce(function (result, item, index) {
    if (index % size > 0) {
      result[result.length - 1].push(item);
    } else {
      result.push([item]);
    }

    return result;
  }, []);
}

/** @tossdocs-ignore */

/**
 * @deprecated This feature is now available in the es-toolkit package.
 */
function clamp(value, bound1, bound2) {
  if (bound2 == null) {
    return Math.min(value, bound1);
  }

  if (bound2 < bound1) {
    throw new Error('The value of bound2 must be a number greater than bound1.');
  }

  return Math.min(Math.max(value, bound1), bound2);
}

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

/**
 * @name copyToClipboard
 * @deprecated clipboard.writeText를 사용하세요.
 **/

function copyToClipboard(text) {
  if (!clipboardCopySupported()) {
    return false;
  }

  copy(text);
  return true;
}

function clipboardCopySupported() {
  var _document$queryComman, _document$queryComman2, _document;

  return (_document$queryComman = (_document$queryComman2 = (_document = document).queryCommandSupported) === null || _document$queryComman2 === void 0 ? void 0 : _document$queryComman2.call(_document, 'copy')) !== null && _document$queryComman !== void 0 ? _document$queryComman : false;
}

function copy(text) {
  var focusingContainer = document.body;
  var textArea = document.createElement('textArea');
  textArea.value = text;
  textArea.contentEditable = 'true';
  textArea.readOnly = false;
  textArea.style.userSelect = 'text';
  textArea.style.webkitUserSelect = 'text';
  focusingContainer.insertBefore(textArea, focusingContainer.firstChild);

  if (isIOS()) {
    var range = document.createRange();
    range.selectNodeContents(textArea);
    var selection = window.getSelection();

    if (selection !== null) {
      selection.removeAllRanges();
      selection.addRange(range);
    }

    textArea.setSelectionRange(0, 999999);
  } else {
    textArea.select();
  }

  document.execCommand('copy');
  focusingContainer.removeChild(textArea);
}

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

/** @tossdocs-ignore */

/**
 * @deprecated This feature is now available in the es-toolkit package.
 */
function delay(milliseconds) {
  return new Promise(function (resolve) {
    setTimeout(resolve, milliseconds);
  });
}

/** @tossdocs-ignore */

/**
 * @deprecated This feature is now available in the es-toolkit package.
 */
function difference(xs, ys) {
  return differenceWith(xs, ys, function (x, y) {
    return x === y;
  });
}
/**
 * @deprecated This feature is now available in the es-toolkit package.
 */

function differenceWith(xs, ys, areItemsEqual) {
  return xs.filter(function (x) {
    return !ys.some(function (y) {
      return areItemsEqual(x, y);
    });
  });
}

/** @tossdocs-ignore */

/**
 * @deprecated This feature is now available in the es-toolkit package.
 */
function escapeHTML(text) {
  return text.replace(/[&<>'"]/g, function (entity) {
    switch (entity) {
      case '&':
        return '&amp;';

      case '<':
        return '&lt;';

      case '>':
        return '&gt;';

      case "'":
        return '&#39;';

      case '"':
        return '&quot;';

      default:
        return entity;
    }
  });
}

/** @tossdocs-ignore */
var nextUniqueId = 0;
/**
 * @deprecated This feature is now available in the es-toolkit package.
 */

function generateID() {
  var prefix = arguments.length > 0 && arguments[0] !== undefined ? arguments[0] : '';
  nextUniqueId = nextUniqueId + 1;
  return "".concat(prefix).concat(nextUniqueId);
}

/** @tossdocs-ignore */

/**
 * @deprecated This feature is now available in the es-toolkit package.
 */
var get = function get(obj, path, defaultValue) {
  var travel = function travel(regexp) {
    return String.prototype.split.call(path, regexp).filter(Boolean).reduce(function (acc, key) {
      return acc !== null && acc !== undefined ? acc[key] : acc;
    }, obj);
  };

  var result = travel(/[,[\]]+?/) || travel(/[,[\].]+?/);
  return result === undefined || result === obj ? defaultValue : result;
};
/**
 * @deprecated This feature is now available in the es-toolkit package.
 */

var set = function set(obj, path, value) {
  // Regex explained: https://regexr.com/58j0k
  var pathArray = path.match(/([^[.\]])+/g);
  pathArray === null || pathArray === void 0 ? void 0 : pathArray.reduce(function (acc, key, i) {
    if (acc[key] == null) {
      acc[key] = {};
    }

    if (i === pathArray.length - 1) {
      acc[key] = value;
    }

    return acc[key];
  }, obj);
  return obj;
};

/** @tossdocs-ignore */
function getScrollYOffset() {
  if (isServer()) {
    return 0;
  }
  /**
   * 보통의 경우 scrollLeft, scrollTop으로 스크롤 위치를 구할 수 있습니다. 하지만 크롬과 파이어폭스의 경우
   * 'document.body' 또는 'document.documentElement' 중 스크롤 위치를 구하는 곳이 다릅니다. 따라서 'scrollTop'
   * 값이 다를 수 있습니다. 'document.documentElement'의 Bounding Rect를 구하는 것은 일관성이 있습니다.
   *
   * 자세한 내용은 이 문서를 참조해 주세요: https://github.com/angular/material2/blob/master/src/cdk/scrolling/viewport-ruler.ts#L102
   */


  var documentElement = document.documentElement;
  var documentRect = documentElement.getBoundingClientRect();
  return -documentRect.top || document.body.scrollTop || window.scrollY || documentElement.scrollTop || 0;
}

/** @tossdocs-ignore */
function getScrollDiffFromBottom() {
  if (isServer() || !document.documentElement) {
    return 0;
  }

  var doc = document.documentElement;
  var body = document.body;
  var scrollTop = getScrollYOffset();
  var scrollBottom = scrollTop + doc.clientHeight;
  var scrollHeight = doc.scrollHeight || body.scrollHeight;
  return scrollHeight - scrollBottom;
}

/** @tossdocs-ignore */
function getScrollPercent() {
  if (isServer() || !document.documentElement) {
    return 0;
  }

  var doc = document.documentElement;
  var body = document.body;
  var scrollTop = getScrollYOffset();
  var scrollHeight = (doc.scrollHeight || body.scrollHeight) - doc.clientHeight;
  return scrollTop / scrollHeight * 100;
}

/** @tossdocs-ignore */
function getViewportSize() {
  if (isServer()) {
    return {
      width: 0,
      height: 0
    };
  }

  return {
    width: window.innerWidth,
    height: window.innerHeight
  };
}

/** @tossdocs-ignore */

/**
 * @deprecated This feature is now available in the es-toolkit package.
 */
function groupBy(data, createKey) {
  return data.reduce(function (result, current) {
    var key = createKey(current);
    var value = result[key];

    if (value == null) {
      result[key] = [current];
    } else {
      value.push(current);
    }

    return result;
  }, {});
}

/** @tossdocs-ignore */
function parseHexValueStr(str) {
  return parseInt(str, 16);
}

function isValidHexValue(hex) {
  var regex = /^#?[0-9A-Fa-f]{6}$/;
  return regex.test(hex);
}
function isRGBDecimalValue(rgbDecimal) {
  return 0 <= rgbDecimal && rgbDecimal <= 255;
}
function isAlphaValue(alpha) {
  return 0 <= alpha && alpha <= 1;
}
function hexToRgba(hex) {
  var alpha = arguments.length > 1 && arguments[1] !== undefined ? arguments[1] : 1;

  if (!isValidHexValue(hex)) {
    throw new Error("Invalid hex value: ".concat(hex));
  }

  if (!isAlphaValue(alpha)) {
    throw new Error("Invalid alpha value(0~1): ".concat(alpha));
  }

  var normalizedHex = hex.startsWith('#') ? hex.slice(1) : hex;
  var r = parseHexValueStr(normalizedHex.slice(0, 2));
  var g = parseHexValueStr(normalizedHex.slice(2, 4));
  var b = parseHexValueStr(normalizedHex.slice(4, 6));
  return "rgba(".concat(r, ",").concat(g, ",").concat(b, ",").concat(alpha, ")");
}

/** @tossdocs-ignore */

/**
 * @deprecated This feature is now available in the es-toolkit package.
 */
function identity(x) {
  return x;
}

/** @tossdocs-ignore */

/**
 * @deprecated This feature is now available in the es-toolkit package.
 */
function isNil(val) {
  return val == null;
}

/** @tossdocs-ignore */

/**
 * @deprecated This feature is now available in the es-toolkit package.
 */
function isNotNil(val) {
  return val != null;
}

/** @tossdocs-ignore */

/**
 * @deprecated This feature is now available in the es-toolkit package.
 */
function mapValues(value, mapper) {
  var entries = Object.entries(value);
  return Object.fromEntries(entries.map(function (_ref) {
    var _ref2 = _slicedToArray(_ref, 2),
        k = _ref2[0],
        v = _ref2[1];

    return [k, mapper(v)];
  }));
}

/** @tossdocs-ignore */
function maskName(name) {
  var _ref = arguments.length > 1 && arguments[1] !== undefined ? arguments[1] : {},
      _ref$maskChar = _ref.maskChar,
      maskChar = _ref$maskChar === void 0 ? '*' : _ref$maskChar;

  var firstLetter = name.slice(0, 1); // 두 글자 이름인 경우, 두 번째 글자를 마스킹해야 하기 때문에
  // 마스킹을 하지 않는 마지막 글자의 최소 시작 인덱스는 2가 된다.

  var lastLetterIndex = Math.max(2, name.length - 1);
  var lastLetter = name.slice(lastLetterIndex);
  var middleLength = name.length - 1 - lastLetter.length;
  var middle = Array.from({
    length: middleLength
  }).fill(maskChar).join('');
  return "".concat(firstLetter).concat(middle).concat(lastLetter);
}

/** @tossdocs-ignore */

/**
 * @deprecated This feature is now available in the es-toolkit package.
 */
function maxBy(collection, iteratee) {
  if (collection.length === 0) {
    return undefined;
  }

  return collection.reduce(function (a, b) {
    return iteratee(a) >= iteratee(b) ? a : b;
  }, {});
}

/** @tossdocs-ignore */

/**
 * @deprecated This feature is now available in the es-toolkit package.
 */
function minBy(collection, iteratee) {
  if (collection.length === 0) {
    return undefined;
  }

  return collection.reduce(function (a, b) {
    return iteratee(a) <= iteratee(b) ? a : b;
  }, {});
}

/** @tossdocs-ignore */
function objectEntries(obj) {
  return Object.entries(obj);
}

/** @tossdocs-ignore */
function objectKeys(obj) {
  return Object.keys(obj);
}

/** @tossdocs-ignore */
function objectValues(obj) {
  return Object.values(obj);
}

/** @tossdocs-ignore */
/**
 * @deprecated This feature is now available in the es-toolkit package.
 */

function omit(obj, keys) {
  return objectKeys(obj).filter(function (k) {
    return !keys.includes(k);
  }).reduce(function (acc, key) {
    return acc[key] = obj[key], acc;
  }, {});
}

/** @tossdocs-ignore */

/**
 * @deprecated This feature is now available in the es-toolkit package.
 */
function pick(obj, keys) {
  var picked = {};

  var _iterator = _createForOfIteratorHelper(keys),
      _step;

  try {
    for (_iterator.s(); !(_step = _iterator.n()).done;) {
      var key = _step.value;
      picked[key] = obj[key];
    }
  } catch (err) {
    _iterator.e(err);
  } finally {
    _iterator.f();
  }

  return picked;
}

/** @tossdocs-ignore */
var 두글자성씨 = ['남궁', '황보', '제갈', '사공', '선우', '서문', '독고', '동방', '어금', '망절', '무본', '황목', '등정', '장곡', '강전'];
var isKorean = /^[ㄱ-ㅎ|ㅏ-ㅣ|가-힣0-9]*$/;
function parseName(koreanName) {
  if (!isKorean.test(koreanName)) {
    return ['', koreanName];
  }

  switch (koreanName.length) {
    case 2:
      return [koreanName.slice(0, 1), koreanName.slice(1)];

    case 3:
      {
        if (두글자성씨.includes(koreanName.slice(0, 2))) {
          return ['', koreanName];
        }

        return [koreanName.slice(0, 1), koreanName.slice(1)];
      }

    default:
      {
        return ['', koreanName];
      }
  }
}

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

/** @tossdocs-ignore */

/**
 * Constructs a query string from the GET parameters provided.
 * Note: If the params object is empty, an empty string is returned. However, if the params object contains key-value pairs, a `?` is prefixed.
 * @example
 * createQueryString({ a: 1, b: 2, c: '가나다' }) // '?a=1&b=2&c=%EA%B0%80%EB%82%98%EB%8B%A4',
 * createQueryString({}) // ''
 * @param  {Params} params The property names must be strings, and the values must be of type string | number | string[] | number[].
 */
function createQueryString(params) {
  var queryString = createSearchParamString(params);

  if (queryString === '') {
    return '';
  }

  return "?".concat(queryString);
}
/**
 * Filters nullable values ​​​​from the provided object and returns them as a string using URLSearchParams.
 * @example
 * createSearchParamString({ foo: 1, bar: ['a', 'b'], baz: undefined }) // foo=1&bar=a&bar=b
 * @param params params The object to convert into a query
 */

function createSearchParamString(params) {
  return new URLSearchParams(Object.entries(params).filter(function (_ref) {
    var _ref2 = _slicedToArray(_ref, 2),
        value = _ref2[1];

    return value != null;
  }).map(function (_ref3) {
    var _ref4 = _slicedToArray(_ref3, 2),
        key = _ref4[0],
        value = _ref4[1];

    if (Array.isArray(value)) {
      return value.map(function (x) {
        return [key, x];
      });
    }

    return [[key, value]];
  }).flat()).toString() // Convert space characters to '%20' according to RFC3986 spec, from RFC1738.
  .replace(/\+/g, '%20');
}
/**
 * Parses a URL query string and returns an object of type 'result'.
 * @param [queryString=location.search] - The query string to parse (`?foo=bar` format), defaults to `location.search`
 * @warn
 * Using parseQueryString without the first parameter is unsafe for SSR.
 */

function parseQueryString() {
  var queryString = arguments.length > 0 && arguments[0] !== undefined ? arguments[0] : typeof location !== 'undefined' ? location.search : '';
  var query = queryString.trim().replace(/^[?#&]/, '');
  return fromEntries(new URLSearchParams(query));
}

function fromEntries(iterable) {
  var result = {};

  for (var _i = 0, _Array$from = Array.from(iterable); _i < _Array$from.length; _i++) {
    var _Array$from$_i = _slicedToArray(_Array$from[_i], 2),
        key = _Array$from$_i[0],
        value = _Array$from$_i[1];

    if (result[key]) {
      if (Array.isArray(result[key])) {
        result[key].push(value);
      } else {
        result[key] = [result[key], value];
      }
    } else {
      result[key] = value;
    }
  }

  return result;
}

function getQueryString(name, parser) {
  var value = QS.parse()[name];

  if (parser == null || value == null) {
    return value;
  } else {
    return parser(value);
  }
}
function setQueryString(search, key, value) {
  var parsed = parseQueryString(search);
  return createQueryString(_objectSpread2(_objectSpread2({}, parsed), {}, _defineProperty({}, key, value)));
}
/**
 * @name QS
 * @description
 * A module that bundles utility functions related to query strings. `QS` exports three utility functions.
 */

var QS = {
  /**
   * @name QS.create (createQueryString)
   * @description
   * Constructs a query string from the provided GET parameters.
   *
   * - The first parameter Record's keys must be strings, and the values can be strings, numbers, arrays of strings, or arrays of numbers.
   * - Note: If params is empty, it returns an empty string, but if there are key-value pairs, it prefixes with a `?`.
   *
   * ```typescript
   * QS.create(
   *   // The object to convert into a query parameter string
   *   obj: Record<string, string | number | string[] | number[]>
   * ): string
   * ```
   *
   * @example
   * QS.create({ a: 1, b: 2, c: '가나다' }) // '?a=1&b=2&c=%EA%B0%80%EB%82%98%EB%8B%A4'
   * QS.create({}) // ''
   */
  create: createQueryString,

  /**
   * @name QS.parse (parseQueryString)
   * @description
   * Parses the URL query string and returns an object of type 'Result'.
   *
   * - Caution: Using QS.parse without the first parameter is unsafe for SSR.
   *
   * ```typescript
   * QS.parse<Result = Record<string, string>>(
   *   // The query string to parse
   *   // @default the value of location.search
   *   queryString: string
   * ): Result
   *
   * @example
   * QS.parse('?prop1=%EC%A0%95%EC%B9%98%20%ED%9B%84%EC%9B%90%EA%B8%88') // { 'prop1': '정치 후원금' }
   */
  parse: parseQueryString,

  /**
   * @name QS.get (getQueryString)
   * @description
   * Gets the value of the current query parameter.
   *
   * ```typescript
   * QS.get<T>(
   *   // Key of query parameters to retrieve
   *   name: string
   * ): T
   * ```
   */
  get: getQueryString,

  /**
   * @name QS.set (setQueryString)
   * Adds a new value to the given query string or modifies an existing value.
   * ```typescript
   * QS.set(
   *   // The query string to modify
   *   qs: string
   *   // The key of the query parameter to add
   *   key: string,
   *   // The value of the query parameter to add
   *   value: string
   * ): string
   * ```
   * @example
   * QS.set('?with_space=hi%20hi', 'referrer', 'foo') // '?with_space=hi%20hi&referrer=foo'
   */
  set: setQueryString
};

/** @tossdocs-ignore */

/**
 * @deprecated This feature is now available in the es-toolkit package.
 */
function range(start, end) {
  var step = arguments.length > 2 && arguments[2] !== undefined ? arguments[2] : 1;

  var _start = end === undefined ? 0 : start;

  var _end = end === undefined ? start : end;

  var output = [];
  var current = _start;

  while (current < _end) {
    output.push(current);
    current += step;
  }

  return output;
}

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

/** @tossdocs-ignore */

/**
 * @deprecated This feature is now available in the es-toolkit package as `invert`.
 */
function reverseKeyValue(obj) {
  return Object.fromEntries(Object.entries(obj).map(function (_ref) {
    var _ref2 = _slicedToArray(_ref, 2),
        key = _ref2[0],
        value = _ref2[1];

    return [value, key];
  }));
}

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

/** @tossdocs-ignore */
var scrollRestoration = {
  set: function set(value) {
    if (!('scrollRestoration' in window.history)) {
      return noop;
    }

    var originalScrollRestoration = window.history.scrollRestoration;
    window.history.scrollRestoration = value;
    return function () {
      window.history.scrollRestoration = originalScrollRestoration;
    };
  }
};

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

/** @tossdocs-ignore */

/**
 * @deprecated This feature is now available in the es-toolkit package.
 */
function shuffle(array) {
  var currentIndex = array.length;
  var randomIndex;
  var newArray = Array.from(array); // While there remain elements to shuffle...

  while (currentIndex !== 0) {
    // Pick a remaining element...
    randomIndex = Math.floor(Math.random() * currentIndex);
    currentIndex--; // And swap it with the current element.

    var _ref = [newArray[randomIndex], newArray[currentIndex]];
    newArray[currentIndex] = _ref[0];
    newArray[randomIndex] = _ref[1];
  }

  return newArray;
}

/** @tossdocs-ignore */

/**
 * @deprecated This feature is now available in the es-toolkit package.
 */
function sum() {
  for (var _len = arguments.length, nums = new Array(_len), _key = 0; _key < _len; _key++) {
    nums[_key] = arguments[_key];
  }

  return nums.flat().reduce(function (acc, curr) {
    return acc + curr;
  }, 0);
}

/** @tossdocs-ignore */
/**
 * @name toInsuranceAge
 * @description
 * 기준일을 기점으로 `보험 나이`를 계산해 줍니다.
 *
 * `보험 나이`란?
 * 보험나이는 실제 만 나이를 기준으로 6개월 미만의 끝수는 버리고 6개월
 * 이상의 끝수는 1년으로 하여 계산하는 나이입니다. 쉽게 생각해 만 나이보다
 * 6개월이 많은 나이라고 할 수 있습니다. 생일보다 6개월이 앞선 날짜를
 * 상령일이라고해서 그 날을 기준으로 보험나이가 오르게 됩니다.
 *
 * ```typescript
 * toInsuranceAge(
 *   // 생년월일 (e.g. '1998-01-01')
 *   date: string,
 *   // 기준일 (e.g. '2022-09-21')
 *   baseDate?: string
 * ): number // => 보험 나이
 * ```
 */

var toInsuranceAge = function toInsuranceAge(date) {
  var baseDate = arguments.length > 1 && arguments[1] !== undefined ? arguments[1] : dateFns.formatISO(new Date());
  var diff = dateFns.differenceInCalendarMonths(dateFns.parseISO(baseDate), dateFns.parseISO(date));
  var baseAge = (diff - diff % 12) / 12;
  var extraAge = diff % 12 < 6 ? 0 : 1;
  return baseAge + extraAge;
};

/** @tossdocs-ignore */
/**
 * @name toInternationalAge
 * @description
 * 기준일을 기점으로 `만 나이`를 계산해 줍니다.
 * ```typescript
 * toInternationalAge(
 *   // 생년월일 (e.g. '1998-01-01')
 *   date: string,
 *   // 기준일 (e.g. '2022-09-21')
 *   baseDate?: string
 * ): number // => 만 나이
 * ```
 */

var toInternationalAge = function toInternationalAge(dateOfBirth) {
  var baseDate = arguments.length > 1 && arguments[1] !== undefined ? arguments[1] : dateFns.formatISO(new Date());
  return dateFns.differenceInYears(dateFns.parseISO(baseDate), dateFns.parseISO(dateOfBirth));
};

/** @tossdocs-ignore */

/**
 * @deprecated This feature is now available in the es-toolkit package.
 */
function uniq(arr) {
  return _toConsumableArray(new Set(arr));
}

/** @tossdocs-ignore */

/**
 * @deprecated This feature is now available in the es-toolkit package.
 */
function uniqBy(arr, hasher) {
  var result = [];
  var addedElements = new Set();

  var _iterator = _createForOfIteratorHelper(arr),
      _step;

  try {
    for (_iterator.s(); !(_step = _iterator.n()).done;) {
      var item = _step.value;
      var hash = hasher(item);

      if (addedElements.has(hash)) {
        continue;
      }

      addedElements.add(hash);
      result.push(item);
    }
  } catch (err) {
    _iterator.e(err);
  } finally {
    _iterator.f();
  }

  return result;
}

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

exports.Masker = Masker;
exports.QS = QS;
exports.arrayIncludes = arrayIncludes;
exports.asyncNoop = asyncNoop;
exports.batchRequestsOf = batchRequestsOf;
exports.ceilToUnit = ceilToUnit;
exports.chunk = chunk;
exports.clamp = clamp;
exports.clipboard = clipboard;
exports.commaizeNumber = commaizeNumber;
exports.copyToClipboard = copyToClipboard;
exports.createMapByKey = createMapByKey;
exports.createQueryString = createQueryString;
exports.createSearchParamString = createSearchParamString;
exports.decommaizeNumber = decommaizeNumber;
exports.delay = delay;
exports.difference = difference;
exports.differenceWith = differenceWith;
exports.escapeHTML = escapeHTML;
exports.floorAndFormatNumber = floorAndFormatNumber;
exports.floorToUnit = floorToUnit;
exports.formatBusinessRegistrationNumber = formatBusinessRegistrationNumber;
exports.formatPhoneNumber = formatPhoneNumber;
exports.formatToKRW = formatToKRW;
exports.formatToKoreanNumber = formatToKoreanNumber;
exports.generateID = generateID;
exports.get = get;
exports.getOSByUserAgent = getOSByUserAgent;
exports.getQueryString = getQueryString;
exports.getScrollDiffFromBottom = getScrollDiffFromBottom;
exports.getScrollPercent = getScrollPercent;
exports.getScrollYOffset = getScrollYOffset;
exports.getViewportSize = getViewportSize;
exports.groupBy = groupBy;
exports.hexToRgba = hexToRgba;
exports.identity = identity;
exports.isAlphaValue = isAlphaValue;
exports.isClient = isClient;
exports.isDifferentArray = isDifferentArray;
exports.isIE = isIE;
exports.isMacOS = isMacOS;
exports.isMobileWeb = isMobileWeb;
exports.isNil = isNil;
exports.isNonEmptyArray = isNonEmptyArray;
exports.isNotNil = isNotNil;
exports.isRGBDecimalValue = isRGBDecimalValue;
exports.isServer = isServer;
exports.isValidHexValue = isValidHexValue;
exports.last = last;
exports.loadScript = loadScript;
exports.mapValues = mapValues;
exports.maskName = maskName;
exports.maxBy = maxBy;
exports.minBy = minBy;
exports.noop = noop;
exports.objectEntries = objectEntries;
exports.objectKeys = objectKeys;
exports.objectValues = objectValues;
exports.omit = omit;
exports.parseName = parseName;
exports.parseQueryString = parseQueryString;
exports.partition = partition;
exports.pick = pick;
exports.range = range;
exports.retryRequestsOf = retryRequestsOf;
exports.reverseKeyValue = reverseKeyValue;
exports.roundToUnit = roundToUnit;
exports.safeScrollTo = safeScrollTo;
exports.safeSmoothScrollTo = safeSmoothScrollTo;
exports.sample = sample;
exports.scrollRestoration = scrollRestoration;
exports.set = set;
exports.setFocusTimeout = setFocusTimeout;
exports.setQueryString = setQueryString;
exports.shuffle = shuffle;
exports.smoothScrollTo = smoothScrollTo;
exports.sum = sum;
exports.toInsuranceAge = toInsuranceAge;
exports.toInternationalAge = toInternationalAge;
exports.uniq = uniq;
exports.uniqBy = uniqBy;
exports.uniqWith = uniqWith;
exports.zip = zip;
