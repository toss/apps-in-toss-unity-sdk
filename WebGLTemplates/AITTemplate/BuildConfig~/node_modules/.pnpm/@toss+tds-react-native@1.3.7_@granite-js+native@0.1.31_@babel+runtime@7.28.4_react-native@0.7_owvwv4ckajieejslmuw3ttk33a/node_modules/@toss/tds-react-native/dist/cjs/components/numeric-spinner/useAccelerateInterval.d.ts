type CallbackFn = () => void;
type Times = {
    startMs: number;
    minMs: number;
    interval: number;
};
export declare function useAccelerateInterval(callback: CallbackFn, times: Times): {
    startInterval: () => void;
    stopInterval: () => void;
};
export {};
