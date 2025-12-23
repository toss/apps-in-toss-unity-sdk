import { TextInput } from 'react-native';
export declare const TextFieldBox: import("react").ForwardRefExoticComponent<import("../../types").TextFieldContainerPublicProps & import("../../types").TextFieldPublicProps & Omit<import("react-native").TextInputProps, "value" | keyof import("../../types").TextFieldPublicProps | keyof import("../../types").TextFieldContainerPublicProps> & {
    value?: import("../../types").TextFieldValue;
    defaultValue?: string;
} & import("react").RefAttributes<TextInput>>;
