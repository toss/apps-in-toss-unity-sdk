/**
 * @param item 반복할 요소
 * @param count 반복할 회수
 * @param param2 {gutter} 반복 사이에 채워넣을 값
 */
export declare function repeat<T>(item: T, count: number, { gutter }?: {
    gutter?: T;
}): Generator<T, void, unknown>;
