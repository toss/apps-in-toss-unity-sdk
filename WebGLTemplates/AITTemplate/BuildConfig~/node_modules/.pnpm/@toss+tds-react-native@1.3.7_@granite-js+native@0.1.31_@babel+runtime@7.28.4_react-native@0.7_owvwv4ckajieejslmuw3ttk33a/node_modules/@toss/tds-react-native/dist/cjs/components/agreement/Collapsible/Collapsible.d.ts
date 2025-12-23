export interface CollapsibleProps {
    open?: boolean;
    defaultOpen?: boolean;
    onOpenChange?: (open: boolean) => void;
}
export interface CollapsibleRef {
    isOpen: boolean;
    open: () => void;
    close: () => void;
    toggle: () => void;
}
export declare const Collapsible: import("react").ForwardRefExoticComponent<CollapsibleProps & {
    children?: import("react").ReactNode | undefined;
} & import("react").RefAttributes<CollapsibleRef>>;
