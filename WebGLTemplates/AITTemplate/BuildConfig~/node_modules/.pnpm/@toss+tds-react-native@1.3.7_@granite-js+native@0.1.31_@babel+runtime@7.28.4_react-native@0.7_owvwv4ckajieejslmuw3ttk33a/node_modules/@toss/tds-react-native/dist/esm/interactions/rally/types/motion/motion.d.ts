import type { Color } from '../motion-input/motion-input-values';
import type { Transition } from '../transitions';
type InterpolationNumberValue = {
    from: number;
    to: number;
};
type InterpolationColorValue = {
    from: Color;
    to: Color;
};
type NumberTransition = InterpolationNumberValue & Transition;
type ColorTransition = InterpolationColorValue & Transition;
export type Motion = {
    width?: NumberTransition;
    height?: NumberTransition;
    perspective?: NumberTransition;
    opacity?: NumberTransition;
    scale?: NumberTransition;
    scaleX?: NumberTransition;
    scaleY?: NumberTransition;
    rotate?: NumberTransition;
    rotateX?: NumberTransition;
    rotateY?: NumberTransition;
    rotateZ?: NumberTransition;
    color?: ColorTransition;
    backgroundColor?: ColorTransition;
    translateX?: NumberTransition;
    translateY?: NumberTransition;
    transformOriginX?: NumberTransition;
    transformOriginY?: NumberTransition;
};
export {};
