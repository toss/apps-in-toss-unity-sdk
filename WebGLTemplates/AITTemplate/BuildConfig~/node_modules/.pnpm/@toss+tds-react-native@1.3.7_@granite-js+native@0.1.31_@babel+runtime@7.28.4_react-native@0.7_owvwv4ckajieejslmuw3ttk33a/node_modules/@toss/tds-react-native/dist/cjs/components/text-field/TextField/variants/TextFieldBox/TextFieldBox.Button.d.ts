import { View } from 'react-native';
export declare const TextFieldBoxButton: import("react").ForwardRefExoticComponent<import("../../types").TextFieldContainerPublicProps & import("../../types").TextFieldPublicProps & Omit<Omit<import("react-native").PressableProps & import("react").RefAttributes<View>, "ref">, "value" | keyof import("../../types").TextFieldPublicProps | keyof import("../../types").TextFieldContainerPublicProps> & {
    value?: import("../../types").TextFieldValue;
    defaultValue?: string;
    focused?: boolean;
    placeholder?: string;
    style?: import("react-native").StyleProp<import("react-native").TextStyle>;
} & import("react").RefAttributes<View>>;
