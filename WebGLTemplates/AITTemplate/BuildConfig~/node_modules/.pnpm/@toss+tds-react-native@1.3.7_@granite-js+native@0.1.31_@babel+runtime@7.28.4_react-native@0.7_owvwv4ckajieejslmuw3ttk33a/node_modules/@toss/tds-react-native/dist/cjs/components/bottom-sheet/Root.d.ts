import type { ComponentType, ReactNode } from 'react';
import React from 'react';
import type { StyleProp, ViewStyle } from 'react-native';
export interface RootProps<WrapperProps> {
    open: boolean;
    header?: ReactNode;
    headerDescription?: ReactNode;
    cta?: React.ReactNode;
    children?: ReactNode;
    disableDimmer?: boolean;
    onDimmerClick?: () => void;
    onClose?: () => void;
    onExited?: () => void;
    style?: StyleProp<ViewStyle>;
    wrapper?: ComponentType<WrapperProps>;
    wrapperProps?: WrapperProps;
    /**
     * 더 큰 텍스트(160% 이상) 환경에서 `header`와 `headerDescription` 영역이 스크롤 내부에 포함되도록 설정해요.
     *
     * @default true
     */
    a11yIncludeHeaderInScroll?: boolean;
    /**
     * @deprecated
     * @description 드래그 기능을 비활성화합니다.
     * @default false
     */
    unstable_disableDragging?: boolean;
}
export declare function BottomSheetRoot({ open, header, headerDescription, cta, disableDimmer, children, onDimmerClick, onClose, onExited, style, wrapper: Wrapper, wrapperProps, a11yIncludeHeaderInScroll, unstable_disableDragging, }: RootProps<object>): import("react/jsx-runtime").JSX.Element;
