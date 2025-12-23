import type { CommonLottieIconProps } from './types';
export interface PresetToastLottieIconProps extends CommonLottieIconProps {
    type: 'complete' | 'error';
}
export declare const PresetToastLottieIcon: ({ type, ...props }: PresetToastLottieIconProps) => import("react/jsx-runtime").JSX.Element;
