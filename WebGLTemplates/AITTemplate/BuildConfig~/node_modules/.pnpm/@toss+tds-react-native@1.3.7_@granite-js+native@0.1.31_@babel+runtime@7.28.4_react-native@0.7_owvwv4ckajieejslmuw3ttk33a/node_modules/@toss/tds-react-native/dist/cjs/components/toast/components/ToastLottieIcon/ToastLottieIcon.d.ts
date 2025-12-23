import { type CustomToastLottieIconProps } from './CustomToastLottieIcon';
import { type PresetToastLottieIconProps } from './PresetToastLottieIcon';
type ToastLottieIconProps = (PresetToastLottieIconProps & {
    preset: true;
}) | (CustomToastLottieIconProps & {
    preset?: false;
});
export declare const ToastLottieIcon: (props: ToastLottieIconProps) => import("react/jsx-runtime").JSX.Element;
export {};
