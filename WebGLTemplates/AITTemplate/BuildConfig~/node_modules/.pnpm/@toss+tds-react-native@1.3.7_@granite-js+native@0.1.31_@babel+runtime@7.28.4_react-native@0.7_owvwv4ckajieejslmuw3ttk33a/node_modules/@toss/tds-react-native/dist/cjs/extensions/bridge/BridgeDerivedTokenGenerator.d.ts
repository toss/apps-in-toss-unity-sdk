import { BaseDerivedTokenGenerator } from '../../core/theme/deriveToken/BaseDerivedTokenGenerator';
import type { SeedToken } from '../../core/theme/seedToken/seedToken';
export type BridgeDerivedTheme = {
    contentLogoShadowColor: string;
    leftRadialGradientColor: string;
    rightRadialGradientColor: string;
    contentTitleStrongColor: string;
    contentTitleDefaultColor: string;
    baseGradientStartColor: string;
    baseGradientMiddleColor: string;
    baseGradientEndColor: string;
    contentTitleStrongInvertedColor: string;
    contentTitleDefaultInvertedColor: string;
    baseGradientStartInvertedColor: string;
    baseGradientMiddleInvertedColor: string;
    baseGradientEndInvertedColor: string;
};
export declare class BridgeDerivedTokenGenerator extends BaseDerivedTokenGenerator<BridgeDerivedTheme> {
    protected calculate(seed: SeedToken): BridgeDerivedTheme;
}
