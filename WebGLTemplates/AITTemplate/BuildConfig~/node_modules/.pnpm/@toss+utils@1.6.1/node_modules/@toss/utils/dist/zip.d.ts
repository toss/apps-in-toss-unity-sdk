declare type ElementOf<T> = T extends Array<infer U> ? U : never;
declare type Zipped<T extends unknown[][]> = Array<{
    [Key in keyof T]: ElementOf<T[Key]>;
}>;
/** @tossdocs-ignore */
/**
 * @deprecated This feature is now available in the es-toolkit package.
 */
export declare function zip<T extends unknown[][]>(...arrays: [...T]): Zipped<T>;
export {};
