type UseButtonLongPressParams = {
    onPressStart?: () => void;
    onPressing: () => void;
    onPressEnd?: () => void;
};
export declare function useButtonLongPress({ onPressStart: _onPressStart, onPressing, onPressEnd: _onPressEnd, }: UseButtonLongPressParams): {
    onPressStart: () => void;
    onPressEnd: () => void;
};
export {};
