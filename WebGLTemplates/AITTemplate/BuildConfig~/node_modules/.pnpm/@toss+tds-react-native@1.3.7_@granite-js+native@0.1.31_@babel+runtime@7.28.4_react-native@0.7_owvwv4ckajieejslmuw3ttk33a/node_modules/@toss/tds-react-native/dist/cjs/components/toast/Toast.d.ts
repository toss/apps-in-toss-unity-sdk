import { type ToastBottomProps } from './components/ToastBottom';
import { type ToastTopProps } from './components/ToastTop';
export type ToastProps = (ToastTopProps & {
    position: 'top';
}) | (ToastBottomProps & {
    position?: 'bottom';
});
export declare const Toast: {
    ({ position, ...props }: ToastProps): import("react/jsx-runtime").JSX.Element;
    Icon: (props: {
        style?: import("react-native").StyleProp<import("react-native").ViewStyle>;
        type?: "default" | "circle" | undefined;
        name: string;
        color?: string | undefined;
    }) => import("react/jsx-runtime").JSX.Element;
    LottieIcon: (props: (import("./components/ToastLottieIcon/PresetToastLottieIcon").PresetToastLottieIconProps & {
        preset: true;
    }) | (import("./components/ToastLottieIcon/CustomToastLottieIcon").CustomToastLottieIconProps & {
        preset?: false;
    })) => import("react/jsx-runtime").JSX.Element;
    Button: ({ children, ...props }: import("./components/ToastButton").ToastButtonProps) => import("react/jsx-runtime").JSX.Element;
};
