import type { ReactNode } from 'react';
import type { ViewProps } from 'react-native';
import type { Placement, ReactElementWithRef } from '../types';
export interface FixedFloatingDropdownProps {
    open?: boolean;
    defaultOpen?: boolean;
    children: ReactElementWithRef;
    dropdown?: ReactNode;
    onOpen?: () => void;
    onClose?: () => void;
    /**
     * @description Trigger를 기준으로 메뉴가 열리는 위치를 정해주세요.
     * @default 'bottom-end'
     * 'bottom-start' | 'bottom-end';
     */
    placement?: Placement;
    containerStyle?: ViewProps['style'];
}
export declare function FixedFloatingDropdown({ children, onOpen, onClose, open, dropdown, containerStyle, }: FixedFloatingDropdownProps): import("react/jsx-runtime").JSX.Element;
