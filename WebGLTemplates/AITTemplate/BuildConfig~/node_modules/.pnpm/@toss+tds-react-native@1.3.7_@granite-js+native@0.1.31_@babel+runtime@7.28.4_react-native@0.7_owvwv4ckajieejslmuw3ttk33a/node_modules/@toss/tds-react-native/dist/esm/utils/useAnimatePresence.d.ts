type PresenceState = 'visible-animation' | 'visible' | 'invisible-animation' | 'invisible';
interface Config {
    enter: () => Promise<void> | void;
    onEntered?: () => void;
    exit: () => Promise<void> | void;
    onExited?: () => void;
}
export declare function useAnimatePresence(visible: boolean, { enter, exit, onEntered, onExited }: Config): {
    presenceState: PresenceState;
};
export {};
