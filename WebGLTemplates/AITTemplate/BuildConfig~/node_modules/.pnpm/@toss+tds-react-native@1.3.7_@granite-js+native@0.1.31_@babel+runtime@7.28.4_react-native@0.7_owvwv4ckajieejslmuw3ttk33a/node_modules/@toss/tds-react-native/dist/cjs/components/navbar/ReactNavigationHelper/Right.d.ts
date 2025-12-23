import type { TxtProps } from '../../txt';
import type { AccessibilityProps, TouchableOpacityProps } from 'react-native';
import { TouchableOpacity } from 'react-native';
type CommonRightProps = TouchableOpacityProps;
declare const RightIconButton: import("react").ForwardRefExoticComponent<Pick<import("../../icon").IconProps & import("react").RefAttributes<import("react-native").View>, "type" | "name"> & AccessibilityProps & TouchableOpacityProps & import("react").RefAttributes<TouchableOpacity>>;
declare function RightTextButton({ children, ...props }: {
    children: TxtProps['children'];
} & CommonRightProps): import("react/jsx-runtime").JSX.Element;
export { RightIconButton, RightTextButton };
