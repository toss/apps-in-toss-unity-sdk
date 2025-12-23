/** @tossdocs-ignore */
import { ElementType } from '@toss/utility-types';
/**
 * @deprecated This feature is now available in the es-toolkit package.
 */
export declare function pick<ObjectType extends Record<PropertyKey, any>, KeyTypes extends Array<keyof ObjectType>>(obj: ObjectType, keys: KeyTypes): Pick<ObjectType, ElementType<KeyTypes>>;
