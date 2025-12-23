import type { ReactElement } from 'react';
export interface UseAnnounceOptions {
    /** 큐에 추가할지 여부 (기본값: false) */
    queue?: boolean;
    /** 지연 시간 (ms, 기본값: 0) */
    delay?: number;
    /** 스크린 리더가 활성화된 경우에만 알림할지 여부 (기본값: true) */
    onlyWhenScreenReaderEnabled?: boolean;
}
/**
 * 스크린 리더 사용자에게 메시지를 알리는 훅
 *
 * @returns announce 함수
 */
export declare function useAnnounce(): {
    announce: (message: string | ReactElement, options?: UseAnnounceOptions) => Promise<void>;
};
