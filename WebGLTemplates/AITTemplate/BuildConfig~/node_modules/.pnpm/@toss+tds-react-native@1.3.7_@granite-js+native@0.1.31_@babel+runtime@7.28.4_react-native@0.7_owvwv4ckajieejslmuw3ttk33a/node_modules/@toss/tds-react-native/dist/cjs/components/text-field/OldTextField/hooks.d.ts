import type { TDSTextFieldSize, TDSTextFieldState } from './TDSTextField';
export declare function useElementStyle(size: TDSTextFieldSize, state: TDSTextFieldState): {
    label: Omit<import("react-native").TextStyle, "color"> & {
        color: import("../../txt").TxtStyleProps["color"];
    };
    input: Omit<import("react-native").TextStyle, "color"> & {
        color: import("../../txt").TxtStyleProps["color"];
    };
    inputContainer: Omit<import("react-native").ViewStyle, "borderBottomColor" | "backgroundColor"> & {
        borderBottomColor: string | undefined;
        backgroundColor: string | undefined;
    };
    helpText: Omit<import("react-native").TextStyle, "color"> & {
        color: import("../../txt").TxtStyleProps["color"];
    };
};
