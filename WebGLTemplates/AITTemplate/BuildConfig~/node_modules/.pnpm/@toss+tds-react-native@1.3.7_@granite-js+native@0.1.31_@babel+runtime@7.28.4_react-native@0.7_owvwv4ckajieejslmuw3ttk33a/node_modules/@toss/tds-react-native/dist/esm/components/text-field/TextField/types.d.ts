import type { ComponentProps, ComponentPropsWithoutRef, ReactNode, Ref } from 'react';
import type { Pressable, StyleProp, TextInput, TextStyle, ViewProps } from 'react-native';
export type TextFieldProps = TextFieldContainerPublicProps & TextFieldPublicProps & InputComponentProps;
export type TextFieldValue = string | number;
export type TextFieldControlProps = Pick<TextFieldProps, 'value' | 'onChangeText' | 'format' | 'hasError' | 'disabled' | 'labelOption'> & {
    editable?: boolean;
};
export type InputComponentProps = Omit<ComponentPropsWithoutRef<typeof TextInput>, keyof TextFieldPublicProps | keyof TextFieldContainerPublicProps | 'value'> & {
    value?: TextFieldValue;
    defaultValue?: string;
};
export type ButtonComponentProps = Omit<ComponentPropsWithoutRef<typeof Pressable>, keyof TextFieldPublicProps | keyof TextFieldContainerPublicProps | 'value'> & {
    value?: TextFieldValue;
    defaultValue?: string;
    /**
     * @type {boolean}
     * @description 텍스트 필드의 포커스 여부
     * @default false
     */
    focused?: boolean;
    placeholder?: string;
    style?: StyleProp<TextStyle>;
};
export type TextAreaComponentProps = Omit<ComponentPropsWithoutRef<'textarea'>, keyof TextFieldPublicProps | keyof TextFieldContainerPublicProps | 'value'> & {
    value?: TextFieldValue;
    defaultValue?: string;
    minHeight?: number | string;
    height?: number | string;
};
export interface TextFieldPublicProps {
    /**
     * @type {boolean}
     * @description 텍스트 필드의 비활성화 여부
     * @default false
     */
    disabled?: boolean;
    /**
     * @type {string}
     */
    prefix?: string;
    /**
     * @type {string}
     */
    suffix?: string;
    /**
     * @type {ReactNode}
     * @description 텍스트 필드의 오른쪽에 위치할 컴포넌트
     */
    right?: ReactNode;
    /**
     * @type {string}
     */
    placeholder?: string;
    /**
     * @type {string}
     * @description 텍스트 필드 컨테이너의 props
     */
    containerProps?: ComponentProps<'div'>;
    containerRef?: Ref<HTMLDivElement>;
    /**
     * @type {{
     *   transform: (value: string) => string;
     *   reset: (formattedValue: string) => string;
     * }}
     *
     * @description 금액, 휴대폰번호 등 특정 형식으로 변환해야 하는 경우 사용합니다.
     * transform: 입력값(value) => 변환된 값
     * reset: 변환된 값 => 입력값(value)
     *
     * @example 금액
     * format={{
     *  transform: value => commaizeNumber(value)
     *  reset: formattedValue => decommaizeNumber(formattedValue)
     * }}
     */
    format?: {
        transform: (value: TextFieldValue) => TextFieldValue;
        reset?: (formattedValue: TextFieldValue) => TextFieldValue;
    };
}
export interface TextFieldPrivateProps {
    focused: boolean;
    hasError: boolean;
    disabled: boolean;
}
export type BaseTextFieldUIProps = Pick<TextFieldPublicProps, 'suffix' | 'prefix' | 'right'> & {
    hasValue: boolean;
    status: 'normal' | 'error' | 'disabled' | 'focused';
};
type BaseTextFieldCommonProps = TextFieldPublicProps & TextFieldPrivateProps & Pick<TextFieldContainerPublicProps, 'variant'>;
export type BaseTextFieldProps = BaseTextFieldCommonProps & Omit<InputComponentProps, 'defaultValue'> & {
    value: TextFieldValue;
};
export type BaseTextFieldButtonProps = Omit<BaseTextFieldCommonProps, 'containerProps' | 'containerRef'> & Omit<ButtonComponentProps, 'defaultValue'>;
export type BaseTextAreaProps = Omit<BaseTextFieldCommonProps, 'prefix' | 'suffix' | 'right'> & Omit<TextAreaComponentProps, 'defaultValue'> & {
    value: TextFieldValue;
};
export type TDSTextFieldVariant = 'box' | 'line' | 'big' | 'hero';
export interface TextFieldContainerPublicProps {
    /**
     * @type {'box' | 'line' | 'big' | 'hero'}
     * @description 텍스트 필드의 모양
     */
    variant: TDSTextFieldVariant;
    /**
     * @type {string}
     * @description 텍스트 필드의 상단의 라벨
     */
    label?: string;
    /**
     * @type {'appear' | 'sustain'}
     * appear: value가 있을 때만 label이 보입니다.
     * sustain: 항상 label이 보입니다.
     * @default appear
     */
    labelOption?: 'appear' | 'sustain';
    /**
     * @type {ReactNode}
     * @description 텍스트 필드의 하단의 도움말
     */
    help?: ReactNode;
    /**
     * @type {boolean}
     * @description 텍스트 필드의 에러 여부
     * @default false
     */
    hasError?: boolean;
    paddingTop?: number;
    paddingBottom?: number;
    containerStyle?: ViewProps['style'];
}
interface TextFieldContainerPrivateProps {
    children: ReactNode;
    focused: boolean;
    hasValue: boolean;
}
export type TextFieldContainerProps = TextFieldContainerPublicProps & TextFieldContainerPrivateProps;
export type ClearableTextFieldProps = Omit<TextFieldProps, 'right'> & {
    onClear?: () => void;
};
export type TextFieldButtonProps = TextFieldContainerPublicProps & TextFieldPublicProps & ButtonComponentProps;
export {};
