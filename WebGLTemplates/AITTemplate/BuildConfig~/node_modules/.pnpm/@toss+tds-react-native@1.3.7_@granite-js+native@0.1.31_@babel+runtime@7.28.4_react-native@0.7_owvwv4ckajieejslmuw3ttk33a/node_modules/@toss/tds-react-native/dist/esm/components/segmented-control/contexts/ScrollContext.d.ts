import type { ReactNode, RefObject } from 'react';
import type { NativeScrollEvent, NativeSyntheticEvent, ScrollView } from 'react-native';
type ScrollHandlers = Set<(event: NativeSyntheticEvent<NativeScrollEvent>) => void>;
interface ScrollContextValues {
    ref: RefObject<ScrollView> | null;
    scrollHandlers: ScrollHandlers;
}
export declare const ScrollContext: import("react").Context<ScrollContextValues>;
interface Props {
    children: ReactNode;
}
export declare function ScrollContextProvider({ children }: Props): import("react/jsx-runtime").JSX.Element;
export {};
