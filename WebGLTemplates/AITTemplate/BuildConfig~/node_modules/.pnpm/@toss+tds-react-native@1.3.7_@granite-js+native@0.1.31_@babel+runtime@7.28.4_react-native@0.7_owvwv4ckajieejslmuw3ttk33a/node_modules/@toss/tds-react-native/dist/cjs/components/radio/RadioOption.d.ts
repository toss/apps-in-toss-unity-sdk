import React from 'react';
export interface RadioOptionProps<Value> {
    value: Value;
    checked?: boolean;
    disabled?: boolean;
    onPress?: (value: Value) => void;
    children: React.ReactNode;
}
declare function RadioOption<Value>({ children, value, checked, disabled, onPress }: RadioOptionProps<Value>): import("react/jsx-runtime").JSX.Element;
export default RadioOption;
