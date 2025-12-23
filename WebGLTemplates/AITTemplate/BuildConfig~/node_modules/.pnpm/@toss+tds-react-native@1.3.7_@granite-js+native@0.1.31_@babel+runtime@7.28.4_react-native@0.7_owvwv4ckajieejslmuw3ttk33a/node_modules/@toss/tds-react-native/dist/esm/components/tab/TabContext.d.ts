export type TabValue = string;
export interface TabContext {
    fluid?: boolean;
    value?: TabValue;
    onChange?: (value: TabValue) => void;
    size: 'large' | 'small';
}
export declare const TabContext: import("react").Context<TabContext>;
