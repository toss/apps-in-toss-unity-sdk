import type { StyleProp, ViewStyle } from 'react-native';
import { View } from 'react-native';
type Props = {
    characters: string[];
    numberOfColumns: number;
    onKeyClick: (value: string) => void;
    onBackspaceClick: () => void;
    forwardedRef: React.Ref<View>;
    style?: StyleProp<ViewStyle>;
};
export declare const VirtualKeypad: import("react").ForwardRefExoticComponent<Omit<Props, "forwardedRef"> & import("react").RefAttributes<View>>;
export {};
