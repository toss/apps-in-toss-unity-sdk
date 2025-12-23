import { View } from 'react-native';
import type { FontWeightKeys, TypographyKeys } from '../../../../constants';
import type { Size, TooltipBaseProps } from '../../types';
export declare const TooltipBase: import("react").ForwardRefExoticComponent<Omit<TooltipBaseProps, "children"> & {
    contentHeight: number;
    left: number;
    top: number;
} & import("react").RefAttributes<View>>;
type TooltipStyle = {
    borderRadius: number;
    paddingVertical: number;
    paddingHorizontal: number;
    anchorHeight: number;
    anchorWidth: number;
    typography: TypographyKeys;
    fontWeight: FontWeightKeys;
};
export declare function getTooltipStyle(size: Size): TooltipStyle;
export {};
