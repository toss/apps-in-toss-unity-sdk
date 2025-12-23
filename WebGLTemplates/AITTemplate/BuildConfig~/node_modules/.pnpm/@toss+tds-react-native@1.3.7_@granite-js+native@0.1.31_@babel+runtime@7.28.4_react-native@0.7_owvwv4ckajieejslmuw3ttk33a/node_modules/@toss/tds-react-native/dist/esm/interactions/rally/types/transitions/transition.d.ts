import type { BezierArray, EasingFn } from './ease';
import type { Spring } from './spring';
export interface SpringTransition {
    easing: Spring;
    duration?: never;
}
export interface BezierTransition {
    /**
     * @default bezier.out
     */
    easing?: EasingFn | BezierArray;
    duration?: number;
}
export type Transition = SpringTransition | BezierTransition;
