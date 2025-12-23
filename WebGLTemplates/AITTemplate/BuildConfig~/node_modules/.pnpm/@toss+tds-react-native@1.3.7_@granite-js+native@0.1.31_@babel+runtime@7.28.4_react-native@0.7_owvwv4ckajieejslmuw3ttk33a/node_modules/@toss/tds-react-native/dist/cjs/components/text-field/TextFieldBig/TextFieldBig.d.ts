import type { ComponentProps } from 'react';
import type { TextInput } from 'react-native';
type TextInputProps = ComponentProps<typeof TextInput>;
export interface Props extends TextInputProps {
    /**
     * 에러 상태를 표시합니다.
     */
    hasError?: boolean;
    /**
     * 우측에 표시될 접미사를 지정합니다.
     */
    suffix?: string;
    /**
     * 텍스트 마스킹 여부를 지정합니다.
     */
    secret?: boolean;
    disabled?: boolean;
    /**
     * input에 입력된 값을 한 번에 지우는 버튼을 노출합니다.
     */
    clearable?: boolean;
    onClear?: () => void;
}
export declare const TextFieldBig: import("react").ForwardRefExoticComponent<Props & import("react").RefAttributes<TextInput>>;
export {};
