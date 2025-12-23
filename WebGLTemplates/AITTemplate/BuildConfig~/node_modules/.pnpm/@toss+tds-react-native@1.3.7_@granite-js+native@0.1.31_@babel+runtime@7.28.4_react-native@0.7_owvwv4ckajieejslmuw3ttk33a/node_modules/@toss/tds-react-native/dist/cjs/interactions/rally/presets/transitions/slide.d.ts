import type { MotionInput, TransitionBasicOptions } from '../../types';
export type SlideOptions = TransitionBasicOptions & {
    hasOpacityMotion?: boolean;
    direction: 'left' | 'right' | 'up' | 'down';
};
export declare function slide({ type, speed, direction, hasOpacityMotion, override, }: SlideOptions): MotionInput;
