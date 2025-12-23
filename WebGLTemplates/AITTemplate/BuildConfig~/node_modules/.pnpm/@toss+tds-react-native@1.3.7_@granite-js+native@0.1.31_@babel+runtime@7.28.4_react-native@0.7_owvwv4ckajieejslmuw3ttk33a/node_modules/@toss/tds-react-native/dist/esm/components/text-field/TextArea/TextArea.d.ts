import type { StyleProp, TextInput, TextStyle } from 'react-native';
import { type TextFieldProps } from '../TextField';
export interface TextAreaProps extends Omit<TextFieldProps, 'variant' | 'multiline'> {
    /**
     *
     * @description fixedHeight를 표현할때 사용합니다
     * @example <TextArea textAreaStyle={{ height: 200 }} />
     */
    textAreaStyle?: StyleProp<TextStyle>;
}
/**
 *
 * @example
 * <TextArea
 *  label="레이블"
 *  placeholder="플레이스홀더"
 *  helpText="도움말입니다"
 *  textAreaStyle={{}}
 * />
 */
export declare const TextArea: import("react").ForwardRefExoticComponent<TextAreaProps & import("react").RefAttributes<TextInput>>;
