import type { MotionInput, TransitionBasicOptions } from '../../types';
export type SlideBounceOptions = TransitionBasicOptions & {
    hasOpacityMotion?: boolean;
    direction: 'left' | 'right' | 'up' | 'down';
};
export declare function slideBounce({ type, speed, direction, hasOpacityMotion, override, }: SlideBounceOptions): MotionInput;
