import type { EffectDirectionOption, EffectTypeOption, MotionInput } from '../../types';
export type WiggleOptions = EffectTypeOption & EffectDirectionOption;
/**
 * 부르르 효과 motion preset
 */
export declare const wiggle: ({ type, direction, override }: WiggleOptions) => MotionInput[];
