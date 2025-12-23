import { RefObject, RefCallback } from 'react';

/**
 * @description
 * This function takes multiple refs (RefObject or RefCallback) and returns a single ref that updates all provided refs.
 * It's useful when you need to pass multiple refs to a single element.
 *
 * @template T - The type of target to be referenced.
 *
 * @param {Array<RefObject<T> | RefCallback<T> | null | undefined>} refs - An array of refs to be merged. Each ref can be either a RefObject or RefCallback.
 *
 * @returns {RefCallback<T>} A single ref callback that updates all provided refs.
 *
 * @example
 * forwardRef(function Component(props, parentRef) {
 *   const myRef = useRef(null);
 *
 *   return <div ref={mergeRefs(myRef, parentRef)} />;
 * })
 *
 * @example
 * function Component(props) {
 *   const ref = useRef(null);
 *   const [height, setHeight] = useState(0);
 *
 *   const measuredRef = useCallback(node => {
 *     if(node == null) {
 *       return;
 *     }
 *
 *     setHeight(node.offsetHeight);
 *   }, []);
 *
 *   return <div ref={mergeRefs(measuredRef, ref)} />;
 * }
 */
declare function mergeRefs<T>(...refs: Array<RefObject<T> | RefCallback<T> | null | undefined>): RefCallback<T>;

export { mergeRefs };
