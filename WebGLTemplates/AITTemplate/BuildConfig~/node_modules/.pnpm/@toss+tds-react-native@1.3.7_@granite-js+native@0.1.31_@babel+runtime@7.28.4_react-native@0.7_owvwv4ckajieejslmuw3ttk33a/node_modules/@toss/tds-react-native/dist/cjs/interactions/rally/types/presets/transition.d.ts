import type { MotionInputProperties } from '../motion-input/motion-input';
export type TransitionType = 'in' | 'out';
export type TransitionSpeed = 'slow' | 'fast';
export type TransitionTypeOption = {
    type: TransitionType;
};
export type TransitionSpeedOption = {
    /** @default 'fast' */
    speed?: TransitionSpeed;
};
export type TransitionBasicOptions = TransitionTypeOption & TransitionSpeedOption & {
    override?: MotionInputProperties;
};
