import type { ComponentProps, ReactElement, ReactNode } from 'react';
import type { AccessibilityProps } from 'react-native';
import { View } from 'react-native';
import { Button } from '../button';
import type { BaseDialogProps } from './BaseDialog';
export interface ConfirmDialogProps extends Pick<BaseDialogProps, 'open' | 'closeOnDimmerClick' | 'onClose' | 'onExited' | 'onEntered'>, AccessibilityProps {
    title: ReactNode;
    description?: ReactNode;
    content?: ReactNode;
    leftButton: ReactElement;
    rightButton: ReactElement;
}
export declare function DoubleButton(props: {
    children: ReactNode;
}): import("react/jsx-runtime").JSX.Element;
declare function DoubleButtonItem(props: ComponentProps<typeof Button>): import("react/jsx-runtime").JSX.Element;
declare const ConfirmDialog: import("react").ForwardRefExoticComponent<ConfirmDialogProps & import("react").RefAttributes<View>> & {
    Button: typeof DoubleButtonItem;
};
export default ConfirmDialog;
