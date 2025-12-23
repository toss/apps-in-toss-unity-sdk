import type { ReactNode } from 'react';
export declare const Childrens: {
    /**
     * @name Childrens.Gap
     * @description ReactNode children 을 렌더할 때 사이에 gap 컴포넌트를 렌더합니다.
     */
    Gap: typeof ChildrensGap;
};
interface ChildrensGapProps {
    children: ReactNode;
    gap: ReactNode;
}
declare function ChildrensGap({ children, gap }: ChildrensGapProps): import("react/jsx-runtime").JSX.Element;
export {};
