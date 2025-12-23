import type { TextFieldControlProps } from '../types';
export declare function useTextFieldValueControls(props: TextFieldControlProps): {
    value: string;
    onChangeText: (text: string) => void;
};
