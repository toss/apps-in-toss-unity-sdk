/** @tossdocs-ignore */
export declare function safeScrollTo(element: Element | Window | null, options: ScrollToOptions): void;
export declare function safeSmoothScrollTo(element: Element | Window, { top: scrollTargetY }: {
    top: number;
}, speed?: number): void;
declare type ScrollConfig = {
    speed: number;
} | {
    duration: number;
};
export declare function smoothScrollTo(element: Element | Window, scrollTo: {
    top: number;
}, config?: ScrollConfig): Promise<void>;
export {};
