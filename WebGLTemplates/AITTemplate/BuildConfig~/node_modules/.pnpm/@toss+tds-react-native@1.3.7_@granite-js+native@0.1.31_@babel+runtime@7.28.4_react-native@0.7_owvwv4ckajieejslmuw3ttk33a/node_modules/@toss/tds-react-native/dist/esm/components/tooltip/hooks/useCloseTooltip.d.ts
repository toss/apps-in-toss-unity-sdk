interface UseCloseTooltipParams {
    open?: boolean;
    duration: number;
    onClose?: () => void;
}
export declare function useCloseTooltip({ open, duration, onClose }: UseCloseTooltipParams): void;
export {};
