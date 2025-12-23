import type { StyleProp, ViewStyle } from 'react-native';
interface Props {
    color: string;
    opacity: number;
    children?: string;
    style?: StyleProp<ViewStyle>;
}
export declare function TextFieldLabel({ color, opacity, children, style }: Props): import("react/jsx-runtime").JSX.Element | null;
export {};
