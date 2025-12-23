import type { ReactNode } from 'react';
import type { StyleProp, ViewStyle } from 'react-native';
interface ContainerProps {
    open: boolean;
    children: ReactNode;
    onExited?: () => void;
    onDraggedUp?: () => void;
    onDraggedMiddle?: () => void;
    onDraggedDown?: () => void;
    onDragToClose?: () => void;
    onDragFinished?: () => void;
    style?: StyleProp<ViewStyle>;
    unstable_disableDragging?: boolean;
}
export declare function BottomSheetContainer({ open, children, onDragFinished, onDragToClose, onDraggedDown, onDraggedMiddle, onDraggedUp, onExited: _onExited, style, unstable_disableDragging, }: ContainerProps): import("react/jsx-runtime").JSX.Element;
export {};
