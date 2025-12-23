import type { EffectTypeOption, MotionInput } from '../../types';
export type PulseOptions = EffectTypeOption;
/**
 * 두근두근 효과 motion preset
 * - Rally에 적용 시 loopMode: 'normal'과 함께 사용해주세요.
 */
export declare const pulse: ({ type, override }: PulseOptions) => MotionInput[];
