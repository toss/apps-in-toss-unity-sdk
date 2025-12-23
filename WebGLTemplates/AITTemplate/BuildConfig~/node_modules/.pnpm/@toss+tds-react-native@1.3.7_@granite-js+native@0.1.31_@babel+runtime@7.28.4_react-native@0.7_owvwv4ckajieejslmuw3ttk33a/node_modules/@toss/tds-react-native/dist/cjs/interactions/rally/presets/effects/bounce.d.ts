import type { EffectTypeOption, MotionInput } from '../../types';
export type BounceOptions = EffectTypeOption;
/**
 * 통통 효과 motion preset
 * - Rally에 적용 시 loopMode: 'normal'로 사용해주세요.
 */
export declare const bounce: ({ type, override }: BounceOptions) => MotionInput[];
