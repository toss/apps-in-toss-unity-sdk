import type { ReactNode, RefObject } from 'react';
import type { View } from 'react-native';
interface TooltipProviderProps {
    open?: boolean;
    children: ReactNode;
    placement?: 'top' | 'bottom';
    offsetNumber?: number;
    /** @TODO */
    autoFlip?: boolean;
}
interface State {
    targetRef: RefObject<View> | null;
    floatingRef: RefObject<View> | null;
    flipped: boolean;
    offset: number;
}
export declare const TooltipContext: import("react").Context<State | null>;
export declare function TooltipProvider({ children, offsetNumber }: TooltipProviderProps): import("react/jsx-runtime").JSX.Element;
export declare function useTooltip(): State | {
    targetRef: null;
    floatingRef: null;
    flipped: null;
    offset: null;
};
export {};
