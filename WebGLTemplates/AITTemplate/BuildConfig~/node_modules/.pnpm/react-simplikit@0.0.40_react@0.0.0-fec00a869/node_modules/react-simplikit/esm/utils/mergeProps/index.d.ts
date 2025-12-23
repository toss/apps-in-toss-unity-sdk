import { CSSProperties } from 'react';

type BaseProps = {
    style?: CSSProperties;
    [key: string]: unknown;
};
type TupleToIntersection<T extends Record<string, unknown>[]> = {
    [I in keyof T]: (x: T[I]) => void;
}[number] extends (x: infer I) => void ? I : never;
/**
 * @description
 * `mergeProps` is a utility function that merges multiple props objects into a single object.
 * It handles merging of `className`, `style`, and `function` properties.
 *
 * @template PropsList - The type of the props objects to merge.
 *
 * @param {PropsList} props - The props objects to merge.
 * @returns {TupleToIntersection<PropsList>} The merged props object.
 *
 * @example
 * const mergedProps = mergeProps({ className: 'foo', style: { color: 'red' } }, { className: 'bar', style: { backgroundColor: 'blue' } });
 * console.log(mergedProps); // { className: 'foo bar', style: { color: 'red', backgroundColor: 'blue' } }
 */
declare function mergeProps<PropsList extends BaseProps[]>(...props: PropsList): TupleToIntersection<PropsList>;

export { mergeProps };
