import type { MotionInputProperties } from '../motion-input/motion-input';
export type EffectType = 'small' | 'big';
export type EffectTypeOption = {
    /** @default 'big' */
    type?: EffectType;
    override?: MotionInputProperties;
};
export type EffectPlayCountOption = {
    repeatType: 'normal' | 'infinite';
};
export type EffectDirectionOption = {
    direction: 'x' | 'y';
};
