import type { TDSTextFieldContainerProps } from './TDSTextFieldContainer';
interface TextFieldSelectButtonProps extends Omit<TDSTextFieldContainerProps, 'children' | 'rightItem'> {
    placeholder: string;
    value: string | undefined;
    onPress: () => void;
}
export declare function TextFieldSelectButton({ value, placeholder, onPress, ...containerProps }: TextFieldSelectButtonProps): import("react/jsx-runtime").JSX.Element;
export {};
