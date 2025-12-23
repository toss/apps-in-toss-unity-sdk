export declare function useTimeout(): {
    clear: () => void;
    set: (fn: () => void, delay: number) => void;
};
