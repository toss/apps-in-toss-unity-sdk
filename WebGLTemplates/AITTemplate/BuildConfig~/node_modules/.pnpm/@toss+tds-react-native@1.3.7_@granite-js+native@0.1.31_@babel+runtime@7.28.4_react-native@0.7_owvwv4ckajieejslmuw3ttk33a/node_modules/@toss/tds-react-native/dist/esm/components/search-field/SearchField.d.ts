import React from 'react';
import type { AccessibilityProps, StyleProp, TextInput, ViewStyle } from 'react-native';
declare const SearchField: React.ForwardRefExoticComponent<{
    placeholder?: string;
    style?: StyleProp<ViewStyle>;
    hasClearButton?: boolean;
} & AccessibilityProps & Pick<import("react-native").TextInputProps, "defaultValue" | "value" | "onChange" | "maxLength" | "autoFocus" | "editable" | "keyboardType" | "placeholder"> & React.RefAttributes<TextInput>>;
export default SearchField;
