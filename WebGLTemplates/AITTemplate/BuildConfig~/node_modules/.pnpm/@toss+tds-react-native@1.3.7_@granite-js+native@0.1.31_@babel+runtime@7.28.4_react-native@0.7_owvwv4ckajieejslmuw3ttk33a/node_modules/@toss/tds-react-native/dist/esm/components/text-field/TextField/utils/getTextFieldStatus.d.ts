import type { TextFieldControlProps } from '../types';
export type TextFieldStatus = 'disabled' | 'error' | 'focused' | 'normal';
export declare function getTextFieldStatus(props: TextFieldControlProps, focused: boolean): TextFieldStatus;
