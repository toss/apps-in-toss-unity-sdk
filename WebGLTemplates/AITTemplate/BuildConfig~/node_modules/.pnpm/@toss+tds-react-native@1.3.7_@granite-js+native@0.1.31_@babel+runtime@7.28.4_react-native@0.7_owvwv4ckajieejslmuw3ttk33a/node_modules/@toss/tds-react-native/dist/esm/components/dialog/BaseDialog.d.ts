import type { ReactElement } from 'react';
import type { AccessibilityProps } from 'react-native';
import { View } from 'react-native';
export interface BaseDialogProps extends AccessibilityProps {
    open: boolean;
    header?: ReactElement;
    body?: ReactElement;
    footer?: ReactElement;
    closeOnDimmerClick?: boolean;
    onClose: () => void;
    /** NOTE: onExited === onCloseAnimationEnd */
    onExited: () => void;
    onEntered?: () => void;
}
declare const BaseDialog: import("react").ForwardRefExoticComponent<BaseDialogProps & import("react").RefAttributes<View>>;
export default BaseDialog;
