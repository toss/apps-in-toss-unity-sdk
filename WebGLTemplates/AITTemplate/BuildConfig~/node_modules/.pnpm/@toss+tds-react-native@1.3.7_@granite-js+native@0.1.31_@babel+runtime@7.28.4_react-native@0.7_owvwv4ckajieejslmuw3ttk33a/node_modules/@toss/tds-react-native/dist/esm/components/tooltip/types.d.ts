import type { ReactElement, RefCallback, RefObject } from 'react';
import type { PressableProps, StyleProp, ViewProps, ViewStyle } from 'react-native';
export type MessageAlign = 'left' | 'center' | 'right';
export type Size = 'small' | 'medium' | 'large';
export type Placement = 'top' | 'bottom';
export interface AnchorProps {
    anchorPositionByRatio?: number;
    /**
     * @description
     * normal Tooltip의 경우, 꼬리는 children의 가운데로 고정됩니다. contentPositionByRatio만큼 content가 움직입니다.
     */
    contentPositionByRatio?: number;
}
export type ReactElementWithRef = ReactElement & {
    ref?: RefObject<any> | RefCallback<any>;
};
export interface TooltipProps extends Pick<AnchorProps, 'contentPositionByRatio'> {
    open?: boolean;
    children: ReactElementWithRef;
    message: string | ReactElement;
    placement?: 'top' | 'bottom';
    messageAlign?: MessageAlign;
    offset?: number;
    size: Size;
    /** @default 'weak' */
    motion?: 'weak' | 'strong';
    /** @TODO */
    autoFlip?: boolean;
    style?: StyleProp<ViewStyle>;
    hidden?: boolean;
    onClose?: () => void;
    onPressOutside?: () => void;
    onChildrenPress?: () => void;
    onPress?: PressableProps['onPress'];
}
export interface TooltipBaseProps extends ViewProps {
    size?: Size;
    motion?: 'weak' | 'strong';
    message?: string | ReactElement;
    background?: ReactElement;
    placement?: Placement;
    anchorMarginLeft?: number;
    messageAlign?: MessageAlign;
    withAnimation?: boolean;
    onPress?: PressableProps['onPress'];
}
export interface FullTooltipBaseProps extends ViewProps {
    message?: string | ReactElement;
    style?: ViewProps['style'];
    /** @TODO */
    withBlur?: boolean;
    background?: ReactElement;
    placement?: 'top' | 'bottom';
    animationDelay?: number;
    anchorMarginLeft?: number;
    messageAlign?: MessageAlign;
    withAnimation?: boolean;
}
export interface FullTooltipProps extends Omit<AnchorProps, 'contentPositionByRatio'> {
    open?: boolean;
    children: ReactElementWithRef;
    message: string | ReactElement;
    /**
     * @default 260
     */
    duration?: number;
    placement?: 'top' | 'bottom';
    messageAlign?: MessageAlign;
    offset?: number;
    /**
     * @description 화살표가 영역에 걸칠 지 결정합니다.
     * offset의 우선순위가 더 높습니다
     *
     */
    anchorOverlapped?: boolean;
    style?: ViewProps['style'];
    onClose?: () => void;
    onPressOutside?: () => void;
}
