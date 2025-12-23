/** @tossdocs-ignore */
/**
 * @deprecated This feature is now available in the es-toolkit package as `invert`.
 */
export declare function reverseKeyValue<KeyType extends string, ValueType extends string>(obj: Record<KeyType, ValueType>): Record<ValueType, KeyType>;
