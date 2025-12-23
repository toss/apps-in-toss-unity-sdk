import type { LottieView } from '@granite-js/native/lottie-react-native';
import type { ComponentProps } from 'react';
export interface CommonLottieIconProps extends Pick<ComponentProps<typeof LottieView>, 'resizeMode' | 'style' | 'loop'> {
    play?: boolean;
    onFinish?: () => void;
}
