import type { EffectPlayCountOption, EffectTypeOption, MotionInput } from '../../types';
export type Float3dOptions = EffectTypeOption & EffectPlayCountOption;
/**
 * 두둥실 효과 motion preset
 * - Rally의 playCount가 infinite면 repeatType infinite, playCount가 상수면 normal로 사용해주세요.
 */
export declare const float3d: ({ type, repeatType, override }: Float3dOptions) => MotionInput[];
