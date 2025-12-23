import type { ComponentProps } from 'react';
import { ContentLottie } from './blocks/ContentLottie';
import type { AssetCommonType } from './types';
type Props = AssetCommonType & ComponentProps<typeof ContentLottie>;
export declare function Lottie({ frameShape, backgroundColor, style, overlap, acc, accPosition, ...lottieProps }: Props): import("react/jsx-runtime").JSX.Element;
export {};
