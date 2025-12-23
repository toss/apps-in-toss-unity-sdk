import type { ReactNode } from 'react';
import type { TxtProps } from '../../components/txt';
export interface NavigationProps {
    children?: ReactNode;
}
export declare function Navigation({ children }: NavigationProps): import("react/jsx-runtime").JSX.Element;
export declare function useBackOrCloseNavigation(): () => void;
export declare function NavigationBackdrop(): import("react/jsx-runtime").JSX.Element;
export type NavigationLeftProps = {
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
export type NavigationRightProps = {
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
export type NavigationCenterProps = {
    children?: ReactNode;
    /**
     * 내비게이션 가운데 영역을 보이거나 숨길 수 있어요.
     * @default true
     */
    visible?: boolean;
};
/**
 * 내비게이션 가운데 영역을 설정할 수 있어요.
 */
export declare function NavigationCenter({ children, visible }: NavigationCenterProps): null;
type Icon = {
    source: {
        uri: string;
    };
    name?: never;
} | {
    name: string;
    source?: never;
};
export type NavigationTitleIcon = Icon;
export declare function NavigationTitleIcon(props: NavigationTitleIcon): import("react/jsx-runtime").JSX.Element;
export type NavigationTitleTextProps = TxtProps;
export declare function NavigationTitleText({ style, ...restProps }: NavigationTitleTextProps): import("react/jsx-runtime").JSX.Element;
export type NavigationBackButtonProps = {
    onPress?: () => void;
    canGoBack?: boolean;
};
export declare function NavigationBackButton({ onPress, canGoBack }: NavigationBackButtonProps): import("react/jsx-runtime").JSX.Element;
export type NavigationCloseButtonProps = {
    onPress?: () => void;
};
export declare function NavigationCloseButton({ onPress }: NavigationCloseButtonProps): import("react/jsx-runtime").JSX.Element;
export {};
