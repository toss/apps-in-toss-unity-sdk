import type { ReactNode } from 'react';
import type { CommonToastProps } from './types';
export interface ToastBottomProps extends CommonToastProps {
    /**
     * @example
     * button={<Toast.Button text="확인" />}
     */
    button?: ReactNode;
    /**
     * @default 20
     * @description px 단위
     */
    bottomOffset?: number;
}
export declare const ToastBottom: ({ open, text, icon, button, duration, bottomOffset, onClose, onExited, onEntered, }: ToastBottomProps) => import("react/jsx-runtime").JSX.Element | null;
