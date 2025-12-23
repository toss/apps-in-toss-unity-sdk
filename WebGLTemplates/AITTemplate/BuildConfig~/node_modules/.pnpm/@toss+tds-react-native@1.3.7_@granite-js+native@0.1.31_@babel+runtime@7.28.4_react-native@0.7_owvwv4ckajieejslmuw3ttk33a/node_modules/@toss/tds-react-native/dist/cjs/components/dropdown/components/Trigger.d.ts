import type { ReactNode } from 'react';
import type { ViewProps } from 'react-native';
import type { Placement, ReactElementWithRef } from '../types';
interface Props {
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
    /**
     * @default 'absolute'
     */
    strategy?: 'fixed' | 'absolute';
}
export declare function Trigger(props: Props): import("react/jsx-runtime").JSX.Element;
export declare namespace Trigger {
    var zIndex: number;
}
export {};
