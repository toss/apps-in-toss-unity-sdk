import { slicedToArray as _slicedToArray, objectSpread2 as _objectSpread2, defineProperty as _defineProperty } from './_virtual/_rollupPluginBabelHelpers.mjs';

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

export { QS, createQueryString, createSearchParamString, getQueryString, parseQueryString, setQueryString };
