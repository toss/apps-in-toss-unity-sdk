import type { ClearableTextFieldProps } from '../types';
export declare function useTextFieldClearButtonControls(props: ClearableTextFieldProps): {
    isVisible: boolean;
    onInputFocus: () => void;
    onInputBlur: () => void;
};
