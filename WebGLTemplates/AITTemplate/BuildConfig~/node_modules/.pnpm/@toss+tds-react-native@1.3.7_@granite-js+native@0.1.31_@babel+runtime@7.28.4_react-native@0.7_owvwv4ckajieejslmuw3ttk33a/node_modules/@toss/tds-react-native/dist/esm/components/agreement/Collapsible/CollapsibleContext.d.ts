export interface CollapsibleContext {
    open: boolean;
    onTrigger: () => void;
}
export declare const CollapsibleContext: import("react").Context<CollapsibleContext>;
