import type { ComponentProps } from 'react';
import { Pressable } from 'react-native';
export interface ToastButtonProps extends ComponentProps<typeof Pressable> {
    children: string;
}
export declare const ToastButton: ({ children, ...props }: ToastButtonProps) => import("react/jsx-runtime").JSX.Element;
