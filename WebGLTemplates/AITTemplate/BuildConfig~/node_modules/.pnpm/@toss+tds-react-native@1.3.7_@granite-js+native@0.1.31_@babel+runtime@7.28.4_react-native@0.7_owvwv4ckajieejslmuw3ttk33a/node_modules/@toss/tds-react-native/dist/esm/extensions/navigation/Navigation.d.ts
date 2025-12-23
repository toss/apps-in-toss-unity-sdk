import type { ReactNode } from 'react';
import { TouchableOpacity, View } from 'react-native';
import type { Icon } from '../../components/icon';
import type { TxtProps } from '../../components/txt';
import type { NavigationTheme } from '../top-navigation';
interface NavigationProps {
    children?: ReactNode;
}
export declare function Navigation({ children }: NavigationProps): import("react/jsx-runtime").JSX.Element;
export declare function useBackOrCloseNavigation(): () => void;
type NavigationLeftProps = {
    children?: ReactNode;
    /**
     * 내비게이션 왼쪽 영역을 보이거나 숨길 수 있어요.
     * @default true
     */
    visible?: boolean;
};
/**
 * 내비게이션 왼쪽 영역을 설정할 수 있어요.
 */
export declare function NavigationLeft({ children, visible }: NavigationLeftProps): null;
type NavigationRightProps = {
    children?: ReactNode;
    /**
     * 내비게이션 오른쪽 영역을 보이거나 숨길 수 있어요.
     * @default true
     */
    visible?: boolean;
};
/**
 * 내비게이션 오른쪽 영역을 설정할 수 있어요.
 */
export declare function NavigationRight({ children, visible }: NavigationRightProps): null;
export declare function NavigationRightWrapper({ children }: {
    children: ReactNode;
}): import("react/jsx-runtime").JSX.Element;
type NavigationContentProps = {
    children?: ReactNode;
    /**
     * 내비게이션 가운데 영역을 보이거나 숨길 수 있어요.
     * @default true
     */
    visible?: boolean;
    background?: boolean;
    /**
     * 타이틀이 화면의 너비만큼의 영역을 차지하고 있어요. 양 끝단의 여백을 설정하여 버튼과 겹치지 않게 할 수 있어요.
     * @default 100
     */
    horizontalSafeMargin?: number;
    onPressTitle?: () => void;
    theme?: 'light' | 'dark';
    withHome?: boolean;
    leftVisible?: boolean;
};
/**
 * 내비게이션 컨텐츠 영역을 설정할 수 있어요.
 */
export declare function NavigationContent({ children, visible, onPressTitle, horizontalSafeMargin, theme, withHome, leftVisible, }: NavigationContentProps): null;
type Icon = {
    source: {
        uri: string;
    };
    name?: never;
} | {
    name: string;
    source?: never;
};
type NavigationTitleIconProps = Icon;
export declare function NavigationTitleIcon(props: NavigationTitleIconProps): import("react/jsx-runtime").JSX.Element | null;
type NavigationTitleTextProps = TxtProps & {
    theme?: NavigationTheme;
};
export declare function NavigationTitleText({ style, theme, ...restProps }: NavigationTitleTextProps): import("react/jsx-runtime").JSX.Element;
type NavigationBackButtonProps = {
    onPress?: () => void;
    canGoBack?: boolean;
    theme?: NavigationTheme;
};
export declare function NavigationBackButton({ onPress, canGoBack, theme }: NavigationBackButtonProps): import("react/jsx-runtime").JSX.Element;
export declare const NavigationIconButton: import("react").ForwardRefExoticComponent<Omit<Pick<import("../..").IconProps & import("react").RefAttributes<View>, "type" | "name"> & import("react-native").AccessibilityProps & import("react-native").TouchableOpacityProps & import("react").RefAttributes<TouchableOpacity>, "ref"> & import("react").RefAttributes<TouchableOpacity>>;
export {};
