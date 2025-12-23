import type { ComponentProps } from 'react';
import { ContentImage } from './blocks/ContentImage';
import type { AssetCommonType } from './types';
type Props = AssetCommonType & ComponentProps<typeof ContentImage>;
export declare function Image({ frameShape, backgroundColor, style, overlap, acc, accPosition, ...imageProps }: Props): import("react/jsx-runtime").JSX.Element;
export {};
