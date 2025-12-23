import type { RadioGroupProps } from '@react-stately/radio';
import type { ReactNode } from 'react';
import type { ViewProps } from 'react-native';
interface Props extends Omit<RadioGroupProps, 'isDisabled'> {
    value: string;
    defaultValue?: string;
    indicator?: ReactNode;
    previousButton?: ReactNode;
    gradation?: ReactNode;
    controller?: ReactNode;
    size?: 'small' | 'large';
    alignment?: 'fixed' | 'fluid';
    name: string;
    disabled?: boolean;
    onChange?: (value: string) => void;
    style?: ViewProps['style'];
    children: ReactNode;
}
export declare function RootBase({ value, defaultValue, indicator, previousButton, gradation, controller, size, alignment: _alignment, disabled, onChange, style, children, ...props }: Props): import("react/jsx-runtime").JSX.Element;
export {};
