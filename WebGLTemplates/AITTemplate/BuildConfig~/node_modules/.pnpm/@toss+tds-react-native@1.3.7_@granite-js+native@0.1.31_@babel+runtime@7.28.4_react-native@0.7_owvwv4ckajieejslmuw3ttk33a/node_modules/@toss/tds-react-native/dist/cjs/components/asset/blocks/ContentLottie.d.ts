import { Lottie } from '@granite-js/react-native';
import type { ComponentProps } from 'react';
import type { AssetResourceScale, AssetResourceScaleType } from '../types';
type LottieProps = Omit<ComponentProps<typeof Lottie>, 'height' | 'resizeMode' | 'autoSize'>;
interface Props extends LottieProps {
    scale?: AssetResourceScale;
    scaleType?: AssetResourceScaleType;
}
export declare function ContentLottie({ scale, scaleType, style, ...lottieProps }: Props): import("react/jsx-runtime").JSX.Element;
export {};
