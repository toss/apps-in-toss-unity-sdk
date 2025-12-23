import type { ViewProps } from 'react-native';
type ImageType = 'default' | 'square' | 'rectangle' | 'rectangle-small' | 'circle' | 'circle-small' | '3d-emoji';
interface BaseProps extends ViewProps {
    /**
     * @description border 표시여부
     * @default false
     */
    border?: boolean;
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
export declare function ListRowImageContainer({ type, border, style, children, ...props }: Props): import("react/jsx-runtime").JSX.Element;
export {};
