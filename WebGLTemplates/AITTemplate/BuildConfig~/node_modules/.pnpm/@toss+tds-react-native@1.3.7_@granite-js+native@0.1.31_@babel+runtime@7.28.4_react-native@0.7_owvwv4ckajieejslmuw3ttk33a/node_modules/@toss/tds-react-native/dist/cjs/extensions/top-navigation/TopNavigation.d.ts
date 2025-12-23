import type { ReactNode } from 'react';
import type { ViewProps } from 'react-native';
import type { Icon } from '../../components/icon';
type Icon = {
    source: {
        uri: string;
    };
    name?: never;
} | {
    name: string;
    source?: never;
};
export interface TopNavigationProps {
    title: string;
    icon: Icon;
    withHome?: boolean;
    onPressTitle?: () => void;
    onPressDots?: () => void;
    onPressClose?: () => void;
    fixedRightButton?: {
        title: string;
        icon: Icon;
        id: string;
        onPress?: () => void;
        disabled?: boolean;
    };
    contentVisible?: boolean;
    children?: ReactNode;
    theme?: NavigationTheme;
    leftVisible?: boolean;
}
export declare function TopNavigation({ title, icon, onPressTitle, onPressDots, onPressClose, fixedRightButton, children, withHome, contentVisible, theme, leftVisible, }: TopNavigationProps): import("react/jsx-runtime").JSX.Element;
export type NavigationTheme = 'light' | 'dark';
export interface NavigationRightContentProps extends ViewProps {
    fixedRightButton: TopNavigationProps['fixedRightButton'];
    onPressDots?: () => void;
    onPressClose?: () => void;
    theme?: NavigationTheme;
}
export declare const COLOR_PALETTE: {
    light: {
        titleTextColor: string;
        backgroundColor: string;
        iconTintColor: string;
        dividerColor: string;
    };
    dark: {
        titleTextColor: string;
        backgroundColor: string;
        iconTintColor: string;
        dividerColor: string;
    };
};
export declare function NavigationRightContent({ fixedRightButton, onPressDots, onPressClose, theme, ...viewProps }: NavigationRightContentProps): import("react/jsx-runtime").JSX.Element;
export declare function CloseWithDotsMono({ onPressDots, onPressClose, theme, }: Pick<TopNavigationProps, 'onPressDots' | 'onPressClose'> & {
    theme?: NavigationTheme;
}): import("react/jsx-runtime").JSX.Element;
export {};
