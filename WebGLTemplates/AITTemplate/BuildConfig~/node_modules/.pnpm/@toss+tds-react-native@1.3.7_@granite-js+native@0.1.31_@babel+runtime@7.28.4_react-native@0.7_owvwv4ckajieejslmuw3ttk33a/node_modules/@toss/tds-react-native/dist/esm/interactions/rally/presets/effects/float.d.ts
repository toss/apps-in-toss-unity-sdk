import type { EffectTypeOption, MotionInput } from '../../types';
export type FloatOptions = EffectTypeOption;
/**
 * 둥둥 효과 motion preset
 * - Rally에 적용 시 loopMode: 'normal'과 함께 사용해주세요.
 */
export declare const float: ({ type, override }: FloatOptions) => MotionInput[];
