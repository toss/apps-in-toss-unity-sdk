import { type PropsWithChildren } from 'react';
import type { ColorPreference } from '../color/ColorPreference';
export interface ColorPreferenceContextValue {
    colorPreference: ColorPreference;
}
export declare const ColorPreferenceContext: import("react").Context<ColorPreferenceContextValue>;
export interface ColorPreferenceProviderProps {
    colorPreference?: ColorPreference;
}
export declare function ColorPreferenceProvider({ colorPreference, children, }: PropsWithChildren<ColorPreferenceProviderProps>): import("react/jsx-runtime").JSX.Element;
export declare function useColorPreference(): ColorPreferenceContextValue;
