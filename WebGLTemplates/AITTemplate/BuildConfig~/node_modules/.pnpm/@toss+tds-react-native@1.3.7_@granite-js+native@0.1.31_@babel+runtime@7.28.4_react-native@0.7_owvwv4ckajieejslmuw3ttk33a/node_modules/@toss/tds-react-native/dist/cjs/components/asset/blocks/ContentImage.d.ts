import { Image } from '@granite-js/react-native';
import type { ComponentProps } from 'react';
import type { AssetResourceScale, AssetResourceScaleType } from '../types';
type ImageProps = ComponentProps<typeof Image>;
interface ImageResourceProps extends Pick<ImageProps, 'source' | 'accessibilityLabel' | 'style'> {
    scale?: AssetResourceScale;
    scaleType?: AssetResourceScaleType;
}
export declare function ContentImage({ source, scale, scaleType, accessibilityLabel, style }: ImageResourceProps): import("react/jsx-runtime").JSX.Element;
export {};
