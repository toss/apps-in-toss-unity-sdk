import type { TimeControl } from '../transitions/time';
import type { BezierTransition, SpringTransition, Transition } from '../transitions/transition';
import type { MotionInputValues, PropertyKey } from './motion-input-values';
export type MotionInputProperty<V = number> = {
    from?: V;
    to?: V;
} & (Partial<SpringTransition> | Partial<BezierTransition>) & TimeControl;
export type MotionInputProperties = {
    [Key in PropertyKey]?: MotionInputProperty<MotionInputValues[Key]>;
};
export type MotionInput = MotionInputProperties & Transition & TimeControl;
