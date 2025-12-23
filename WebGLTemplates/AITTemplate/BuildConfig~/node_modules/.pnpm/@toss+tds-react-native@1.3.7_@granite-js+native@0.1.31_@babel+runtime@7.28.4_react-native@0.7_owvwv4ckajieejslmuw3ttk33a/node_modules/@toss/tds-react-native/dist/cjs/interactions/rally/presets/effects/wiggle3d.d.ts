import type { EffectDirectionOption, EffectTypeOption, MotionInput } from '../../types';
export type Wiggle3dOptions = EffectTypeOption & EffectDirectionOption;
/**
 * 출렁출렁 효과 motion preset
 * - Rally에 적용 시 loopMode: 'normal'과 함께 사용해주세요.
 */
export declare const wiggle3d: ({ type, direction, override }: Wiggle3dOptions) => MotionInput[];
