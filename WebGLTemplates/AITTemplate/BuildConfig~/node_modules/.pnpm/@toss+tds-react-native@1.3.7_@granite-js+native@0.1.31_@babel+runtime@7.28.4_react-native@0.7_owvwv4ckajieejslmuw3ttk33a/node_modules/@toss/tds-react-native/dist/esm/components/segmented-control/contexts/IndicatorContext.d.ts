import type { ReactNode } from 'react';
interface Options {
    indicatorX: number;
    indicatorWidth: number;
    setIndicatorX: (x: number) => void;
    setIndicatorWidth: (width: number) => void;
}
export declare const IndicatorContext: import("react").Context<Options>;
interface Props {
    children: ReactNode;
}
export declare function IndicatorContextProvider({ children }: Props): import("react/jsx-runtime").JSX.Element;
export {};
