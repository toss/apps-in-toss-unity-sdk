export interface IndicatorContext {
    width?: number;
    translateX?: number;
    setWidth?: (width: number) => void;
    setTranslateX?: (translateX: number) => void;
}
export declare const IndicatorContext: import("react").Context<IndicatorContext>;
