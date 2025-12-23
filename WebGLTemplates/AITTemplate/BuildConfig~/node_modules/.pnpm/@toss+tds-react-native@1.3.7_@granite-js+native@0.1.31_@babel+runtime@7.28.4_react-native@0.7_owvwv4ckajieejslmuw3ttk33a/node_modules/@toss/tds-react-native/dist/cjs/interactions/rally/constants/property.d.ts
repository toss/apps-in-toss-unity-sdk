import type { Color, Degree, TransformOrigin } from '../types';
type DefaultStyles = {
    opacity: number;
    scale: number;
    scaleX: number;
    scaleY: number;
    rotate: Degree;
    rotateX: Degree;
    rotateY: Degree;
    rotateZ: Degree;
    translateX: number;
    translateY: number;
    transformOriginX: TransformOrigin;
    transformOriginY: TransformOrigin;
    color: Color;
    backgroundColor: Color;
};
/**
 * 기본 스타일 값
 */
export declare const defaultStyles: DefaultStyles;
export declare const defaultDuration = 500;
export declare const defaultEasing: import("@toss/tds-easings").CubicBezier;
export {};
