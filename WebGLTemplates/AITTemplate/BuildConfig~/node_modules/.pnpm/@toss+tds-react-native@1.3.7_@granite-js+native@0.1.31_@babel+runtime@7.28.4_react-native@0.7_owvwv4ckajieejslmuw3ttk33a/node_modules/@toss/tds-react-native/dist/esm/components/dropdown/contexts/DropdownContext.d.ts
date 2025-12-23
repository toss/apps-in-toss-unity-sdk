import type { ReactNode, RefObject } from 'react';
import type { View } from 'react-native';
import type { Placement } from '../types';
interface DropdownProviderProps {
    open?: boolean;
    placement: Placement;
    children: ReactNode;
    onPressOutside?: () => void;
}
interface State {
    dropdownBaseRef: RefObject<View> | null;
    targetRef: RefObject<View> | null;
    placement: Placement;
}
export declare const DropdownContext: import("react").Context<State | null>;
export declare function DropdownProvider({ placement, children }: DropdownProviderProps): import("react/jsx-runtime").JSX.Element;
export declare function useDropdown(): State;
export {};
