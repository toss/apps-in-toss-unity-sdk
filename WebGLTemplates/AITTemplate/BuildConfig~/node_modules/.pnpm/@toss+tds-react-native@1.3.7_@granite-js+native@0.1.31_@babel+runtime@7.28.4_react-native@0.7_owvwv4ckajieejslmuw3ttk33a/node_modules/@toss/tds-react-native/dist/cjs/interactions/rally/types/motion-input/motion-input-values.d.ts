export type RelativeOperator = '+=' | '-=' | '*=' | '/=';
export type RelativeNumber = `${RelativeOperator}${number}`;
export type Length = `${number}px` | number;
export type RelativeLength = `${RelativeOperator}${Length}`;
export type Percentage = `${number}%`;
export type RelativePercentage = `${RelativeOperator}${Percentage}`;
export type TransformOriginKeyword = 'bottom' | 'center' | 'left' | 'right' | 'top';
export type TransformOrigin = TransformOriginKeyword | Length | Percentage;
export type RelativeTransformOrigin = RelativeLength | RelativePercentage;
export type Degree = number | `${number}deg`;
export type RelativeDegree = `${RelativeOperator}${Degree}`;
export type Color = `#${string}` | `rgb(${number}, ${number}, ${number})` | `rgba(${number}, ${number}, ${number}, ${number})` | string;
export interface MotionInputValues {
    backgroundColor?: Color;
    color?: Color;
    opacity?: number | RelativeNumber;
    /** px, % */
    width?: Length | Percentage | RelativeLength | RelativePercentage;
    /** px, % */
    height?: Length | Percentage | RelativeLength | RelativePercentage;
    transformOriginX?: TransformOrigin | RelativeTransformOrigin;
    transformOriginY?: TransformOrigin | RelativeTransformOrigin;
    /**
     * width에 대한 함수, px
     */
    perspective?: Length | ((width: number) => number);
    scale?: number | RelativeNumber;
    scaleX?: number | RelativeNumber;
    scaleY?: number | RelativeNumber;
    /**
     * px, %.
     * 상대값 지원.
     * @example 기존값 10px, 지정한 값 -=10px: 최종 0
     */
    translateX?: Length | Percentage | RelativeLength | RelativePercentage;
    /**
     * px, %.
     * 상대값 지원.
     * @example 기존값 10px, 지정한 값 -=10px: 최종 0
     */
    translateY?: Length | RelativeLength | Percentage | RelativePercentage;
    rotate?: Degree | RelativeDegree;
    rotateX?: Degree | RelativeDegree;
    /**
     * 0-360 deg
     * 상대값 지원.
     * @example 기존값 60deg, 지정한 값 -=90deg: 최종 -30deg
     */
    rotateY?: Degree | RelativeDegree;
    /**
     * 0-360 deg.
     * 상대값 지원.
     * @example 기존값 60deg, 지정한 값 -=90deg: 최종 -30deg
     */
    rotateZ?: Degree | RelativeDegree;
}
export type PropertyKey = keyof MotionInputValues;
