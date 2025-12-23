import type { ReactNode } from 'react';
import type { GestureResponderEvent } from 'react-native';
type EventNames = 'touchEnd' | 'touchStart';
type GestureHandler = (event: GestureResponderEvent, scrolled?: boolean) => void;
interface GlobalHandlers {
    addEventListener: (eventName: EventNames, handler: GestureHandler) => void;
    removeEventListener: (eventName: EventNames, handler: GestureHandler) => void;
}
interface Props {
    children: ReactNode;
}
export declare function useGlobalEvent(): GlobalHandlers;
/**
 * Global 하게 적용될 Event 를 주입합니다.
 * tooltip 과 같이 pressOutside 영역이 필요한 경우 사용됩니다.
 */
export declare function GlobalEventProvider({ children }: Props): import("react/jsx-runtime").JSX.Element;
export {};
