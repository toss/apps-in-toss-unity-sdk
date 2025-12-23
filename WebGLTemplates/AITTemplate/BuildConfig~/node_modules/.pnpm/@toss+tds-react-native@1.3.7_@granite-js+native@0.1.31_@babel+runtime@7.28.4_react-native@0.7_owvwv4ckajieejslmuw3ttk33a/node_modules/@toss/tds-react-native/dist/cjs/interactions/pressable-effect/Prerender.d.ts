import type { PropsWithChildren, ReactNode } from 'react';
interface Props {
    initial: ReactNode;
}
/**
 * 초기 로딩 성능 개선을 위해 가벼운 컴포넌트를 먼저 그리고 CPU가 바쁘지 않을 때 무거운 컴포넌트를 그립니다
 * @param initial 가벼운 컴포넌트
 */
export declare function Prerender({ initial, children }: PropsWithChildren<Props>): import("react/jsx-runtime").JSX.Element;
export {};
