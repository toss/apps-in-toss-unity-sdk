interface Props {
    open: boolean;
    onPress?: () => void;
}
export interface BottomSheetDimmerRef {
    darken: () => void;
    lighten: (percent: number) => void;
    reset: () => void;
}
export declare const BottomSheetDimmer: import("react").ForwardRefExoticComponent<Props & import("react").RefAttributes<BottomSheetDimmerRef>>;
export {};
