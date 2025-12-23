import { ReactNavigationNavbar } from '../../components/navbar';
import type { ComponentProps } from 'react';
type IconProps = ComponentProps<typeof ReactNavigationNavbar.RightIconButton>;
interface TextButtonProps {
    type: 'text';
    title: string;
    onClick: () => void;
}
interface IconButtonProps {
    type: 'icon';
    name: IconProps['name'];
    iconType?: IconProps['type'];
    alt: string;
    onClick: () => void;
}
interface IconButtonProps {
    type: 'icon';
}
type Props = TextButtonProps | IconButtonProps;
/**
 * @deprecated `<PageNavbar.AccessoryIconButton />`, `<PageNavbar.AccessoryTextButton />` 을 사용해주세요.
 */
export declare function CompatAccessoryButton(props: Props): import("react/jsx-runtime").JSX.Element;
export {};
