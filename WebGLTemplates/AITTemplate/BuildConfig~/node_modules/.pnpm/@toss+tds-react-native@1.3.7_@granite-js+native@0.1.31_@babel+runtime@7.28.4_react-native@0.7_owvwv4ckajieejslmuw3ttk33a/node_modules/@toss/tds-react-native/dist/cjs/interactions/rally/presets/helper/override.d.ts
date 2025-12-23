import type { MotionInput, MotionInputProperties } from '../../types';
type MotionInputsOverride = {
    [index: number]: MotionInputProperties;
};
export declare const overrideEffect: (motions: MotionInput[], overrideMap?: MotionInputsOverride) => any[];
export declare const overrideTransition: (motion: MotionInput, override?: MotionInputProperties) => any;
export {};
