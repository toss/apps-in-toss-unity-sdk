import type { TouchableOpacityProps } from 'react-native';
interface CloseButtonProps extends TouchableOpacityProps {
    tintColor?: string;
    onPress?: () => void;
}
declare function CloseButton({ tintColor, onPress }: CloseButtonProps): import("react/jsx-runtime").JSX.Element;
export { CloseButton };
