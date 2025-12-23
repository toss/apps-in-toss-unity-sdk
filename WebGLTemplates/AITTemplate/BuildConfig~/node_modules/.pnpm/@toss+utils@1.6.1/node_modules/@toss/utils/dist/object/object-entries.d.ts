/** @tossdocs-ignore */
import { ObjectKeys } from './object-keys.js';
export declare function objectEntries<Type extends Record<PropertyKey, unknown>>(obj: Type): Array<[ObjectKeys<Type>, Type[ObjectKeys<Type>]]>;
