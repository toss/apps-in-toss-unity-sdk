import type { ComponentProps } from 'react';
import { View } from 'react-native';
export type BorderType = 'full' | 'padding24' | 'height16';
interface BaseProps {
    type?: BorderType;
    height?: number | undefined;
}
interface HairlineProps extends BaseProps {
    type: 'full' | 'padding24';
    height?: undefined;
}
interface SpaceProps extends BaseProps {
    type: 'height16';
    height?: number;
}
type Props = (HairlineProps | SpaceProps) & ComponentProps<typeof View>;
declare function Border({ type, style, height, ...restProps }: Props): import("react/jsx-runtime").JSX.Element;
export default Border;
