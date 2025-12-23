import type { StyleProp, TouchableOpacityProps, ViewStyle } from 'react-native';
import type { TDSTextFieldSize } from './TDSTextField';
export declare function TDSTextFieldItem({ style, children }: {
    style?: StyleProp<ViewStyle>;
    children?: React.ReactNode;
}): import("react/jsx-runtime").JSX.Element;
export declare namespace TDSTextFieldItem {
    var Label: typeof TDSTextFieldLabelItem;
    var Icon: typeof TDSTextFieldIconItem;
}
declare function TDSTextFieldLabelItem({ label, style, textfieldSize, }: {
    label: string;
    textfieldSize?: TDSTextFieldSize;
    style?: StyleProp<ViewStyle>;
}): import("react/jsx-runtime").JSX.Element;
declare function TDSTextFieldIconItem({ style, name, size, color, onPress, }: {
    name: string;
    onPress?: TouchableOpacityProps['onPress'];
    size?: number;
    style?: StyleProp<ViewStyle>;
    color?: string;
}): import("react/jsx-runtime").JSX.Element;
export {};
