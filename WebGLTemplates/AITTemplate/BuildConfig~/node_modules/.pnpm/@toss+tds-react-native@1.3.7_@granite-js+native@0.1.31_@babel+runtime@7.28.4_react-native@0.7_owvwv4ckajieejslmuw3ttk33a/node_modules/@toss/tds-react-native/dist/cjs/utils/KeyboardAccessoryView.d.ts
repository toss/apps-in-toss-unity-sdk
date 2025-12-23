import type { StyleProp, ViewProps, ViewStyle } from 'react-native';
interface Props extends ViewProps {
    contentContainerStyle?: StyleProp<ViewStyle>;
    enabled?: boolean;
    keyboardVerticalOffset?: number;
}
/**
 * @name KeyboardAccessoryView
 * @description 컴포넌트를 키보드 위로 띄워줍니다.
 * - react-native 의 KeyboardAvoidingView 를 사용하면 onLayout 시점과 keyboardEvent 시점이 달라 높이가 달라지는 버그가 있습니다.
 */
export declare function KeyboardAccessoryView({ style, contentContainerStyle, enabled, children, onLayout, keyboardVerticalOffset, ...props }: Props): import("react/jsx-runtime").JSX.Element;
export {};
