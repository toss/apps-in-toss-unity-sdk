import type { EffectTypeOption, MotionInput } from '../../types';
export type RippleOptions = EffectTypeOption;
/**
 * 잔물결 효과 motion preset
 * - Rally에 적용 시 loopMode: 'normal'와 함께 사용해주세요.
 */
export declare const ripple: ({ type, override }: RippleOptions) => MotionInput[];
