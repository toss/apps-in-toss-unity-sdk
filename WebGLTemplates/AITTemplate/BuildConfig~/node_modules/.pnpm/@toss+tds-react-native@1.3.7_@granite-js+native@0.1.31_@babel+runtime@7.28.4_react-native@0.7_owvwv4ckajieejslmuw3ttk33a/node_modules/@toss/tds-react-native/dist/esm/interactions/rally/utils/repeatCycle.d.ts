/**
 * @param items 반복 시 요소들 각각에도 적용할 cycle items
 * @param count 반복 회수
 * @param { gutter } 중간에 넣을 값
 *
 * @example
 * ```ts
 *  repeatCycle([1, 2], 3, { gutter: 0 });
 *  // [1, 0, 2, 0, 1]
 * ```
 */
export declare function repeatCycle<T>(items: T[], count: number, { gutter }?: {
    gutter?: T;
}): Generator<T | undefined, void, unknown>;
