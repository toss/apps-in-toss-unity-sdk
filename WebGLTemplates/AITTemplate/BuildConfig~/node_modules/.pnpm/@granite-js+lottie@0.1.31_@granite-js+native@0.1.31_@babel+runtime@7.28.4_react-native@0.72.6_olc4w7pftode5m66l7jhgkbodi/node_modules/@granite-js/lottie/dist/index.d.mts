import * as react_jsx_runtime from 'react/jsx-runtime';
import LottieView, { AnimationObject } from '@granite-js/native/lottie-react-native';
import { ComponentProps } from 'react';

type RemoteLottieProps = BaseProps & {
    src: string;
};
type AnimationObjectLottieProps = BaseProps & {
    animationObject: AnimationObject;
};
type LottieReactNativeProps = ComponentProps<typeof LottieView>;
type BaseProps = Omit<LottieReactNativeProps, 'source'> & {
    /**
     * Height is required to prevent layout shifting.
     */
    height: number | '100%';
    width?: number | '100%';
    maxWidth?: number;
};
declare function Lottie({ width, maxWidth, height, src, autoPlay, speed, style, onAnimationFailure, ...props }: RemoteLottieProps): react_jsx_runtime.JSX.Element;
declare namespace Lottie {
    var AnimationObject: ({ width, maxWidth, height, animationObject, autoPlay, speed, style, onAnimationFailure, ...props }: AnimationObjectLottieProps) => react_jsx_runtime.JSX.Element;
}

export { type AnimationObjectLottieProps, Lottie, type RemoteLottieProps };
