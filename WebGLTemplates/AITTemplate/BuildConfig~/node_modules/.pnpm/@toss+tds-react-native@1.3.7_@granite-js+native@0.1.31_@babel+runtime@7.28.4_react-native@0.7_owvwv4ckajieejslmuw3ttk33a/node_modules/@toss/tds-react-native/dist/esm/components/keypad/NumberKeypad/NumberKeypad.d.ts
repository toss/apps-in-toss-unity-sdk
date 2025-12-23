import type { StyleProp, View, ViewStyle } from 'react-native';
export type NumberKey = 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9;
export type NumberKeypadRef = React.Ref<View>;
export type NumberKeypadProps = {
    /**
     * 숫자 키패드의 순서
     * - 1, 2, 3, 4, 5, 6, 7, 8, 9, 0
     */
    numbers?: NumberKey[];
    /**
     * Key를 클릭하면 발생하는 이벤트
     *
     * @param value string
     */
    onKeyPress: (value: number) => void;
    /**
     * Backspace를 클릭하면 발생하는 이벤트
     */
    onBackspacePress: () => void;
    forwardedRef: NumberKeypadRef;
    style?: StyleProp<ViewStyle>;
};
export declare function NumberKeypadComponent({ numbers, onKeyPress, onBackspacePress, forwardedRef, style, }: NumberKeypadProps): import("react/jsx-runtime").JSX.Element;
export declare const NumberKeypad: import("react").ForwardRefExoticComponent<Omit<NumberKeypadProps, "forwardedRef"> & import("react").RefAttributes<View>>;
