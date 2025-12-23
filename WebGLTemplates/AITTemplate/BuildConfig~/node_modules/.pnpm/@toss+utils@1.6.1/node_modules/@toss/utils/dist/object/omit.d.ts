/** @tossdocs-ignore */
import { ElementType } from '@toss/utility-types';
import { ObjectKeys } from './object-keys';
/**
 * @deprecated This feature is now available in the es-toolkit package.
 */
export declare function omit<ObjectType extends Record<PropertyKey, any>, KeyTypes extends Array<ObjectKeys<ObjectType>>>(obj: ObjectType, keys: KeyTypes): Omit<ObjectType, ElementType<KeyTypes>>;
