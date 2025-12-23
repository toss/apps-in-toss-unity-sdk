/**
 * 현재 타임스탬프를 관리합니다
 * `active` 상태가 true일 때 타임스탬프를 현재 시간으로 업데이트하고,
 * false일 때 타임스탬프를 0으로 초기화합니다.
 */
export declare function useTimestampRef({ active }: {
    active: boolean;
}): import("react").MutableRefObject<number>;
