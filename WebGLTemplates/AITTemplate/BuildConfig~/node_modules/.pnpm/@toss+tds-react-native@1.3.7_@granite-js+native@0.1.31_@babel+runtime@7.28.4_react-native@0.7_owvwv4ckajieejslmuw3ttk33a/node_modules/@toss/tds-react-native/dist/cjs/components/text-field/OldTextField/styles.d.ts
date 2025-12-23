import type { TextStyle, ViewStyle } from 'react-native';
import type { TxtStyleProps } from '../../txt';
import type { TDSTextFieldSize, TDSTextFieldState } from './TDSTextField';
type TextStyleByVariant = Record<`${TDSTextFieldSize}-${TDSTextFieldState}`, Omit<TextStyle, 'color'> & {
    color: TxtStyleProps['color'];
}>;
export declare const getLabelStyle: ({ grey700, grey800 }: {
    grey700: string;
    grey800: string;
}) => TextStyleByVariant;
export declare const getHelpTextStyle: ({ grey500, grey600, grey700, }: {
    grey500: string;
    grey600: string;
    grey700: string;
}) => TextStyleByVariant;
type ViewStyleByVariant = Record<`${TDSTextFieldSize}-${TDSTextFieldState}`, Omit<ViewStyle, 'borderBottomColor' | 'backgroundColor'> & {
    borderBottomColor: string | undefined;
    backgroundColor: string | undefined;
}>;
export declare const getInputContainerStyle: ({ grey50, grey100, grey200, }: {
    grey50: string;
    grey100: string;
    grey200: string;
}) => ViewStyleByVariant;
export declare const fontFamilyMap: Record<"bold" | "thin" | "extraLight" | "light" | "normal" | "regular" | "medium" | "semiBold" | "extraBold" | "heavy" | "black" | "extralight" | "semibold" | "extrabold", string>;
export declare const getInputStyle: ({ grey400, grey900 }: {
    grey400: string;
    grey900: string;
}) => TextStyleByVariant;
export {};
