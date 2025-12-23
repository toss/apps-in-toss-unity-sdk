import type { ForwardedRef, LegacyRef, MutableRefObject, Ref } from 'react';
export declare function mergeRefs<T = any>(...refs: Array<MutableRefObject<T> | LegacyRef<T> | ForwardedRef<T>>): Ref<T>;
