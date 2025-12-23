import type { StyleProp, TextInput, TextStyle } from 'react-native';
import type { TDSTextFieldProps } from './TDSTextField';
interface TextAreaProps extends Omit<TDSTextFieldProps, 'size' | 'multiline' | 'textInputStyle'> {
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
declare const TextArea: import("react").ForwardRefExoticComponent<TextAreaProps & import("react").RefAttributes<TextInput>>;
export default TextArea;
