import type { ComponentProps } from 'react';
import { Frame } from './blocks/Frame';
import { ResourceText } from './Resource/ResourceText';
import type { AssetCommonType } from './types';
type TextProps = ComponentProps<typeof ResourceText>;
type FrameProps = ComponentProps<typeof Frame>;
type BaseProps = TextProps & AssetCommonType;
type Props = BaseProps & {
    frameShape: FrameProps['shape'];
};
export declare function Text({ frameShape, backgroundColor, color, size, children, ...frameProps }: Props): import("react/jsx-runtime").JSX.Element;
export {};
