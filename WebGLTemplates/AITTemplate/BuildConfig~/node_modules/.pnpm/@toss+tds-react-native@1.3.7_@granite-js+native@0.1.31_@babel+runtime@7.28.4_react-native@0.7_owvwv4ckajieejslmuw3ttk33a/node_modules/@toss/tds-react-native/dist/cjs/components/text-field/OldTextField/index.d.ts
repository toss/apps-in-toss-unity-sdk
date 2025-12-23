import { TDSTextFieldItem } from './TDSTextFieldItem';
import { TextFieldSelectButton } from './TDSTextFieldSelectButton';
/** @deprecated */
declare const OldTextField: import("react").ForwardRefExoticComponent<Omit<import("react-native").TextInputProps, "clearButtonMode"> & Omit<import("./TDSTextFieldContainer").TDSTextFieldContainerProps, "children" | "focus" | "onPress"> & Pick<import("./TDSTextFieldContent").TDSTextFieldContentProps, "leftItem" | "rightItem"> & {
    contentStyle?: import("react-native").StyleProp<import("react-native").ViewStyle>;
} & import("react").RefAttributes<import("react-native").TextInput>>;
/** @deprecated */
declare const OldTextFieldItem: typeof TDSTextFieldItem;
/** @deprecated */
declare const OldTextFieldSelectButton: typeof TextFieldSelectButton;
export { OldTextField, OldTextFieldItem, OldTextFieldSelectButton };
