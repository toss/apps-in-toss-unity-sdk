import { Image } from '@granite-js/react-native';
import { Icon } from '../icon';
import type { ComponentProps } from 'react';
type IconType = 'default' | 'small' | 'border' | 'background';
type IconProps = Omit<ComponentProps<typeof Icon>, 'size'>;
type ImageProps = ComponentProps<typeof Image>;
interface BaseProps {
    /** @default default */
    type?: IconType;
    /**
     * @default 'center';
     */
    align?: 'top' | 'center';
}
interface NamedIconProps extends BaseProps {
    name: IconProps['name'];
    color?: IconProps['color'];
    style?: IconProps['style'];
}
interface UrlIconProps extends BaseProps {
    /**
     * react-native-fast-image 의 source 입니다.
     */
    source: ImageProps['source'];
    style?: ImageProps['style'];
}
type Props = NamedIconProps | UrlIconProps;
declare function ListRowIcon({ type, align, style, ...restProps }: Props): import("react/jsx-runtime").JSX.Element;
export { ListRowIcon };
