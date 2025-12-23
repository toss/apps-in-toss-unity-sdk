import React from 'react';
import type { DerivedToken } from './deriveToken';
import type { SeedToken } from './seedToken';
type DeepPartial<T> = {
    [P in keyof T]?: T[P] extends object ? DeepPartial<T[P]> : T[P];
};
export type ThemeToken = SeedToken & DerivedToken;
export declare const ThemeContext: React.Context<{
    token: ThemeToken;
} | null>;
export interface ThemeProviderProps {
    token?: DeepPartial<ThemeToken>;
    children: React.ReactNode;
}
export declare function ThemeProvider({ token: customSeedToken, children }: ThemeProviderProps): import("react/jsx-runtime").JSX.Element;
export declare function useTheme(): {
    token: ThemeToken;
};
export {};
