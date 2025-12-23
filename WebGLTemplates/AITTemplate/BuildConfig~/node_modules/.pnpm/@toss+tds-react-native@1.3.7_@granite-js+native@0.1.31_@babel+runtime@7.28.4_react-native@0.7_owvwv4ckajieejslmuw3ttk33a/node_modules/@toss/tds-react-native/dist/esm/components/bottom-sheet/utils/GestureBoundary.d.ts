import type { ReactNode } from 'react';
interface Props {
    children: ReactNode;
}
/**
 * 중첩된 Gesture Handler 사용 시, Gesture 끼리의 충돌을 막기 위해 사용
 * (CTA 버튼을 클릭하더라도 BottomSheet가 갑자기 작아졌다가 커지는 현상 방지)
 */
export declare function BottomSheetGestureBoundary({ children }: Props): import("react/jsx-runtime").JSX.Element;
export {};
