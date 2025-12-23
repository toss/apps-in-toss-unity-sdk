import type { ComponentProps, ReactNode } from 'react';
import type { StyleProp, TextStyle } from 'react-native';
import { Text } from 'react-native';
import type { FontWeightKeys, TypographyKeys } from '../../constants';
export interface TxtStyleProps {
    /**
     * @default 't5'
     */
    typography?: TypographyKeys;
    /**
     * @default 'regular'
     */
    fontWeight?: FontWeightKeys;
    /**
     * @default adaptive.grey900
     */
    color?: string;
    numberOfLines?: number;
    textAlign?: TextStyle['textAlign'];
}
export type TxtProps = TxtStyleProps & ComponentProps<typeof Text> & {
    children: ReactNode;
    style?: StyleProp<TextStyle>;
};
export declare function toFontWeightStyle(fontWeightKey: FontWeightKeys): {
    fontWeight: "100" | "200" | "300" | "400" | "500" | "600" | "700" | "800" | "900";
    fontFamily: string;
};
declare const Txt: import("react").ForwardRefExoticComponent<TxtStyleProps & import("react-native").TextProps & {
    children: ReactNode;
    style?: StyleProp<TextStyle>;
} & import("react").RefAttributes<Text>>;
export declare const fontFamilyByFontWeightMap: Record<'android' | 'ios', Record<FontWeightKeys, string>>;
export default Txt;
