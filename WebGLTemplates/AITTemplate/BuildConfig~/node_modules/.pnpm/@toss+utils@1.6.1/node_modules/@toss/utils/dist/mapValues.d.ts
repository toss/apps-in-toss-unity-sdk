/** @tossdocs-ignore */
/**
 * @deprecated This feature is now available in the es-toolkit package.
 */
export declare function mapValues<T extends Record<PropertyKey, any>, U>(value: T, mapper: (value: T[Exclude<keyof T, symbol>]) => U): {
    [K in Exclude<keyof T, symbol>]: U;
};
