import { ReactNavigationNavbar } from '../../components/navbar';
import type { ComponentProps, ReactElement } from 'react';
type TitleTxtProps = ComponentProps<typeof ReactNavigationNavbar.TitleTxt>;
type HeaderTitleProps = Partial<ComponentProps<typeof ReactNavigationNavbar.HeaderTitle>>;
interface BaseProps extends HeaderTitleProps {
    withHeaderLeft?: boolean;
}
interface PageTitleProps extends BaseProps {
    children: ReactElement;
    color?: never;
    fontWeight?: never;
    preventFontScalingA11y?: never;
}
interface PageTitleStringProps extends BaseProps, TitleTxtProps {
    children: string;
}
type Props = PageTitleProps | PageTitleStringProps;
export declare function PageTitle({ children, withHeaderLeft, style, ...titleTxtProps }: Props): null;
export {};
