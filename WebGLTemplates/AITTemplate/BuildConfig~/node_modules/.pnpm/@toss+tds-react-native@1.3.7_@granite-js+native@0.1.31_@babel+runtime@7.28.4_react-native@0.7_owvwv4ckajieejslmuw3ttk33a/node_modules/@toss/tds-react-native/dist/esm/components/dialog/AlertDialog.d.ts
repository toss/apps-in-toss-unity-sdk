import type { ReactNode } from 'react';
import type { AccessibilityProps } from 'react-native';
import type { BaseDialogProps } from './BaseDialog';
export interface AlertDialogProps extends Pick<BaseDialogProps, 'open' | 'closeOnDimmerClick' | 'onClose' | 'onExited' | 'onEntered'>, AccessibilityProps {
    title: ReactNode;
    description?: ReactNode;
    content?: ReactNode;
    /**
     * @default '확인'
     */
    buttonText?: string;
    onButtonPress?: () => void;
}
/**
 *
 * @example
 * import { useOverlay } from '../../../core';
 *
 * const overlay = useOverlay();
 * const openAlertDialog = () => {
 *   return new Promise<void>(resolve => {
 *     overlay.open(({ isOpen, close, exit }) => (
 *       <AlertDialog
 *         open={isOpen}
 *         closeOnDimmerClick={false}
 *         title="제목"
 *         description="설명"
 *         onClose={() => {
 *           resolve();
 *           close();
 *         }}
 *         onExited={exit}
 *       />
 *     ));
 *   });
 * };
 */
declare function AlertDialog({ open: isOpen, title, description, content, buttonText, closeOnDimmerClick, onClose, onExited, onButtonPress, onEntered, ...restProps }: AlertDialogProps): import("react/jsx-runtime").JSX.Element;
export default AlertDialog;
