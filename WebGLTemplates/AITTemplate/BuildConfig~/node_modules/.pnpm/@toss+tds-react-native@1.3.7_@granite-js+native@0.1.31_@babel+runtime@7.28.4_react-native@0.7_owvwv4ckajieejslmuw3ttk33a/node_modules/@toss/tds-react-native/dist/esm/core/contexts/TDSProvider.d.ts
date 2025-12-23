import { type PropsWithChildren } from 'react';
import type { ThemeProviderProps } from '../theme/ThemeProvider';
import type { ColorPreferenceProviderProps } from './ColorPreferenceProvider';
import type { TypographyThemeProviderProps } from './TypographyProvider';
type Props = ColorPreferenceProviderProps & TypographyThemeProviderProps & ThemeProviderProps & {
    /**
     * 더큰텍스트 지원 여부
     * 기본값은 false로 더큰텍스트 설정을 따르지 않아요.
     */
    fontScaleAvailable?: boolean;
};
export declare function TDSProvider({ colorPreference, typographyLevel, children, token, fontScaleAvailable, }: PropsWithChildren<Props>): import("react/jsx-runtime").JSX.Element;
export {};
