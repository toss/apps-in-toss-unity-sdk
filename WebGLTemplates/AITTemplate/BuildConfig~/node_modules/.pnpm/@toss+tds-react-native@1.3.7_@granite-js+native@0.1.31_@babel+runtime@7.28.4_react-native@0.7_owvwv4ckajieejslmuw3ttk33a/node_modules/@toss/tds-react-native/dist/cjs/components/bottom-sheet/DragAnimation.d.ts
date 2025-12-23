import type { ComponentProps, ReactNode } from 'react';
import type { View } from 'react-native';
interface Props extends ComponentProps<typeof View> {
    children: ReactNode;
    onDraggedUp?: () => void;
    onDraggedDown?: () => void;
    onDragToClose?: () => void;
    onDraggedMiddle?: () => void;
    onDragFinished?: () => void;
    unstable_disableDragging?: boolean;
}
export declare function BottomSheetDragAnimation({ children, onDraggedUp, onDraggedDown, onDraggedMiddle, onDragToClose, onDragFinished, unstable_disableDragging, ...props }: Props): import("react/jsx-runtime").JSX.Element;
export {};
