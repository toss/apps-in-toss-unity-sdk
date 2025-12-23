import type { TypographyLevel, TypographyMap } from '../../constants';
import { type PropsWithChildren } from 'react';
export interface TypographyTheme {
    typography: TypographyMap;
    typographyLevel: TypographyLevel;
}
export declare const defaultTypographyThemeValue: TypographyTheme;
export declare const TypographyThemeContext: import("react").Context<TypographyTheme>;
export interface TypographyThemeProviderProps {
    /**
     * TDS에 전달할 `TypographyLevel`을 설정할 수 있어요.
     *
     * 이 값을 설정하면 앱의 기본 글꼴 설정을 무시하고 고정된 값으로 동작해요.
     * 예를 들어, `typographyLevel`을 'Large'로 설정하면, 앱 설정과 상관없이 항상 'Large'로 표시돼요.
     *
     * 이 값을 설정하지 않으면, 사용자가 앱 설정에서 글꼴 크기를 변경할 때
     * TDS에 전파되는 `TypographyLevel`도 동적으로 변경돼요.
     */
    typographyLevel?: TypographyLevel;
}
/**
 * Typography 를 제공하는 Provider
 */
export declare function TypographyThemeProvider({ typographyLevel, children, }: PropsWithChildren<TypographyThemeProviderProps>): import("react/jsx-runtime").JSX.Element;
export declare function useTypographyTheme(): TypographyTheme;
