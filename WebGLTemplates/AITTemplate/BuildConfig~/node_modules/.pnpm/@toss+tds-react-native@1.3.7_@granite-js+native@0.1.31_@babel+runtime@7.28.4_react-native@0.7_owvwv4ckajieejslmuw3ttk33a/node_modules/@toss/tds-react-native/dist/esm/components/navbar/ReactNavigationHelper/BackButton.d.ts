import type { TouchableOpacityProps } from 'react-native';
interface BackButtonProps extends TouchableOpacityProps {
    tintColor?: string;
    onPress?: () => void;
}
declare function BackButton({ tintColor, ...restProps }: BackButtonProps): import("react/jsx-runtime").JSX.Element;
export { BackButton };
