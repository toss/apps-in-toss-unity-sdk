import type { TextInput, View } from 'react-native';
import type { TextFieldProps } from './types';
import { format } from './utils/format';
declare const _TextField: import("react").ForwardRefExoticComponent<import("./types").TextFieldContainerPublicProps & import("./types").TextFieldPublicProps & Omit<import("react-native").TextInputProps, "value" | keyof import("./types").TextFieldPublicProps | keyof import("./types").TextFieldContainerPublicProps> & {
    value?: import("./types").TextFieldValue;
    defaultValue?: string;
} & import("react").RefAttributes<TextInput>>;
declare const _ClearableTextField: import("react").ForwardRefExoticComponent<Omit<TextFieldProps, "right"> & {
    onClear?: () => void;
} & import("react").RefAttributes<TextInput>>;
declare const _TextFieldButton: import("react").ForwardRefExoticComponent<import("./types").TextFieldContainerPublicProps & import("./types").TextFieldPublicProps & Omit<Omit<import("react-native").PressableProps & import("react").RefAttributes<View>, "ref">, "value" | keyof import("./types").TextFieldPublicProps | keyof import("./types").TextFieldContainerPublicProps> & {
    value?: import("./types").TextFieldValue;
    defaultValue?: string;
    focused?: boolean;
    placeholder?: string;
    style?: import("react-native").StyleProp<import("react-native").TextStyle>;
} & import("react").RefAttributes<View>>;
type ExportedTextField = typeof _TextField & {
    Clearable: typeof _ClearableTextField;
    format: typeof format;
    Button: typeof _TextFieldButton;
};
export declare const TextField: ExportedTextField;
export {};
