import { BaseDerivedTokenGenerator } from '../../core/theme/deriveToken/BaseDerivedTokenGenerator';
import type { SeedToken } from '../../core/theme/seedToken/seedToken';
export type ButtonDerivedTheme = {
    backgroundFillColor: string;
    textFillColor: string;
    backgroundWeakColor: string;
    textWeakColor: string;
    dimFillColor: string;
    dimWeakColor: string;
    gradientStartFillColor: string;
    gradientStartWeakColor: string;
    gradientEndFillColor: string;
    gradientEndWeakColor: string;
    loaderFillColor: string;
    loaderWeakColor: string;
};
export declare class ButtonDerivedTokenGenerator extends BaseDerivedTokenGenerator<ButtonDerivedTheme> {
    protected calculate(seed: SeedToken): ButtonDerivedTheme;
}
