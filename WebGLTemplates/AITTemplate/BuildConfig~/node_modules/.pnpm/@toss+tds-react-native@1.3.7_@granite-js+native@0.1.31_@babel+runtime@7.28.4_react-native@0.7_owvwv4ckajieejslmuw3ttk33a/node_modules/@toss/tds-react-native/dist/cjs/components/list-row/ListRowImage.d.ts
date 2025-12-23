import { Image } from '@granite-js/react-native';
import type { ComponentProps } from 'react';
import type { ViewProps } from 'react-native';
type ImageProps = ComponentProps<typeof Image>;
type ImageType = 'default' | 'square' | 'rectangle' | 'rectangle-small' | 'circle' | 'circle-small' | '3d-emoji';
interface BaseProps {
    /**
     * @description border 표시여부. "rectangle", "rectangle-small" 은 기본값이 true
     * @default false
     */
    border?: boolean;
    source: ImageProps['source'];
    resizeMode?: ImageProps['resizeMode'];
    style?: ViewProps['style'];
}
interface ImageTypeProps {
    type: Exclude<ImageType, 'default'>;
}
interface DefaultTypeProps {
    type: 'default';
    /**
     * @description LayoutShift 를 방지하기 위한 기본 width 입니다.
     */
    width: number;
    /**
     * @description LayoutShift 를 방지하기 위한 기본 height 입니다.
     */
    height: number;
}
type Props = (ImageTypeProps | DefaultTypeProps) & BaseProps;
declare function ListRowImage({ type, border, resizeMode, source, style, ...props }: Props): import("react/jsx-runtime").JSX.Element;
export { ListRowImage };
