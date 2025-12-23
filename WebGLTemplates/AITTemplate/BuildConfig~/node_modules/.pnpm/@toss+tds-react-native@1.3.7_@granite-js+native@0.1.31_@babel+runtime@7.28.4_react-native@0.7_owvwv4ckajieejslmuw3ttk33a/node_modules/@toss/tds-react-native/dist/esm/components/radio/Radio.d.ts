import React from 'react';
import type { Control, FieldPathValue, FieldValues, Path, UseControllerProps } from 'react-hook-form';
import type { RadioOptionProps } from './RadioOption';
import RadioOption from './RadioOption';
interface RadioProps<Value> {
    /**
     * The value of the radio option that is selected.
     */
    value: Value;
    /**
     * The callback that is called when the user selects a new radio option.
     * @param value The value of the radio option that is selected.
     * @returns void
     */
    onChange: (value: Value) => void;
    /**
     * disabled 여부
     * @default false
     */
    disabled?: boolean;
    /**
     * The horizontal margin to apply to the radio component.
     * @default 0
     * @type number
     */
    horizontalMargin?: number;
    /**
     * The children of the radio component.
     * @type Radio.Option | Radio.Option[]
     * @required true
     */
    children: React.ReactElement<RadioOptionProps<Value>> | Array<React.ReactElement<RadioOptionProps<Value>>>;
}
/**
 * TDS Radio component. To use Radio as a input field with __useForm__, use the {@link RadioInput} component instead.
 * @template Value - The value of the radio option
 * @param RadioProps props { children, value, onChange, horizontalMargin = 0 }. See {@link RadioProps}
 */
declare const Radio: {
    <Value>({ value, children, disabled, onChange, horizontalMargin }: RadioProps<Value>): import("react/jsx-runtime").JSX.Element;
    Option: typeof RadioOption;
};
interface RadioInputProps<FormData extends FieldValues, TName extends Path<FormData>, Value extends FieldPathValue<FormData, TName>> extends Omit<RadioProps<Value>, 'onChange' | 'value' | 'children'> {
    /**
     * @typedef { import('react-hook-form').UseControllerProps } UseControllerProps
     */
    /**
     * The current selected value in the radio's options
     * @type {UseControllerProps}
     */
    controlerProps?: Omit<UseControllerProps<FormData, TName>, 'control' | 'name'>;
    control: Control<FormData>;
    name: TName;
    onChange?: (value: Value) => void;
    children: React.ReactNode | React.ReactNode[];
}
/**
 * TDS Radio component which work with __useForm__.
 * @see {@link Radio} for more information
 *
 * @export
 * @template Value - The value of the radio option
 * @param RadioInputProps props { children, controlerProps, onChange, horizontalMargin = 0 }. See {@link RadioInputProps}
 * @example
 * const { control, handleSubmit } = useForm();
 * ...
 * <RadioInput control={control} name="field1">
 *   <Radio.Option value="1">Option 1</Radio.Option>
 *   <Radio.Option value="2">Option 2</Radio.Option>
 * </RadioInput>
 */
export declare function RadioInput<FormData extends FieldValues, TName extends Path<FormData>, Value extends FieldPathValue<FormData, TName>>({ controlerProps, control, name, children, ...rest }: RadioInputProps<FormData, TName, Value>): import("react/jsx-runtime").JSX.Element;
export { Radio };
