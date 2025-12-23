import type { EffectPlayCountOption, EffectTypeOption, MotionInput } from '../../types';
export type ShiverOptions = EffectTypeOption & EffectPlayCountOption;
/**
 * 흔들흔들 효과 motion preset
 * - Rally에 적용 시 loopMode: 'normal'과 함께 사용해주세요.
 * - Rally의 playCount가 infinite면 repeatType infinite, playCount가 상수면 normal로 사용해주세요.
 */
export declare const shiver: ({ type, repeatType, override }: ShiverOptions) => MotionInput[];
