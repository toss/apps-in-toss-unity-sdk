/** @tossdocs-ignore */
import { ObjectKeys } from './object-keys';
export declare function objectValues<Type extends Record<PropertyKey, unknown>>(obj: Type): Array<Type[ObjectKeys<Type>]>;
