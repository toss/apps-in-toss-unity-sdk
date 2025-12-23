import { Icon } from '../../icon';
import type { ComponentProps } from 'react';
type ToastIconProps = Pick<ComponentProps<typeof Icon>, 'name' | 'color' | 'style' | 'type'>;
export declare const ToastIcon: (props: ToastIconProps) => import("react/jsx-runtime").JSX.Element;
export {};
