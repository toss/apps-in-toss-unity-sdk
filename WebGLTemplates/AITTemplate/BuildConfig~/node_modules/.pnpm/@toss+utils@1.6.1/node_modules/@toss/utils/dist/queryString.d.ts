/** @tossdocs-ignore */
/**
 * Constructs a query string from the GET parameters provided.
 * Note: If the params object is empty, an empty string is returned. However, if the params object contains key-value pairs, a `?` is prefixed.
 * @example
 * createQueryString({ a: 1, b: 2, c: '가나다' }) // '?a=1&b=2&c=%EA%B0%80%EB%82%98%EB%8B%A4',
 * createQueryString({}) // ''
 * @param  {Params} params The property names must be strings, and the values must be of type string | number | string[] | number[].
 */
export declare function createQueryString(params: Record<string, any>): string;
/**
 * Filters nullable values ​​​​from the provided object and returns them as a string using URLSearchParams.
 * @example
 * createSearchParamString({ foo: 1, bar: ['a', 'b'], baz: undefined }) // foo=1&bar=a&bar=b
 * @param params params The object to convert into a query
 */
export declare function createSearchParamString(params: Record<string, any>): string;
/**
 * Parses a URL query string and returns an object of type 'result'.
 * @param [queryString=location.search] - The query string to parse (`?foo=bar` format), defaults to `location.search`
 * @warn
 * Using parseQueryString without the first parameter is unsafe for SSR.
 */
export declare function parseQueryString<Result = Record<string, string>>(queryString?: string): Result;
export declare function getQueryString(name: string): string | undefined;
export declare function getQueryString<T>(name: string, parser: (val: string) => T): T | undefined;
export declare function setQueryString(search: string, key: string, value: string): string;
/**
 * @name QS
 * @description
 * A module that bundles utility functions related to query strings. `QS` exports three utility functions.
 */
export declare const QS: {
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
    create: typeof createQueryString;
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
    parse: typeof parseQueryString;
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
    get: typeof getQueryString;
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
    set: typeof setQueryString;
};
