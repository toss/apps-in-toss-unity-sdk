export interface SelectProps {
    options: Array<{
        name: string;
        value: string;
        disabled?: boolean;
        hideUnCheckedCheckBox?: boolean;
    }>;
    value?: string;
    animation?: boolean;
    animationDelay?: number;
    onChange: (value: string) => void;
}
export declare function BottomSheetSelect({ options, value, animation, animationDelay, onChange }: SelectProps): import("react/jsx-runtime").JSX.Element;
