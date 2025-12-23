/** @tossdocs-ignore */
export declare type ObjectKeys<T extends Record<PropertyKey, unknown>> = `${Exclude<keyof T, symbol>}`;
export declare function objectKeys<Type extends Record<PropertyKey, unknown>>(obj: Type): Array<ObjectKeys<Type>>;
