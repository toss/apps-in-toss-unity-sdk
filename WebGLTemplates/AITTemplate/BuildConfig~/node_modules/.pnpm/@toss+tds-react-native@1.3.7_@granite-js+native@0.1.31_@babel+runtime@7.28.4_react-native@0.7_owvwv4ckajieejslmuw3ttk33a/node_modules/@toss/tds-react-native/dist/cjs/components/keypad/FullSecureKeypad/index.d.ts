import type { StyleProp, ViewStyle } from 'react-native';
import { View } from 'react-native';
export interface FullSecureKeypadProps {
    /**
     * Key를 클릭하면 발생하는 이벤트
     *
     * @param value string
     */
    onKeyPress: (value: string) => void;
    /**
     * Backspace를 클릭하면 발생하는 이벤트
     */
    onBackspacePress: () => void;
    /**
     * Space를 클릭하면 발생하는 이벤트
     */
    onSpacePress: () => void;
    /**
     * 입력 완료를 클릭하면 발생하는 이벤트
     */
    onSubmit: () => void;
    submitDisabled?: boolean;
    submitButtonText?: string;
    open?: boolean;
    style?: StyleProp<ViewStyle>;
}
export type FullSecureKeypadRef = {
    reorderEmptyCells: () => void;
    element: View | null;
};
export declare const FullSecureKeypad: import("react").MemoExoticComponent<import("react").ForwardRefExoticComponent<FullSecureKeypadProps & import("react").RefAttributes<FullSecureKeypadRef>>>;
