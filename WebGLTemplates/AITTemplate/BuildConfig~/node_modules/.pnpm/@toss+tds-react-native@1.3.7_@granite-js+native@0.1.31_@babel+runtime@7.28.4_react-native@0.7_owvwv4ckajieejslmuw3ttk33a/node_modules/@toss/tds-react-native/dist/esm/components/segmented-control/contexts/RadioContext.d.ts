import type { ReactNode } from 'react';
import type { LayoutRectangle } from 'react-native';
interface RadioGroupState {
    name: string;
    selectedValue: string | undefined;
    setSelectedValue: (value: string) => void;
    disabled: boolean;
}
interface Options extends RadioGroupState {
    selectedElementLayout: LayoutRectangle | null;
    setSelectedElementLayout: (layout: LayoutRectangle) => void;
}
export declare const RadioContext: import("react").Context<Options>;
interface Props extends RadioGroupState {
    children: ReactNode;
}
export declare function RadioContextProvider({ children, ...radioContextValue }: Props): import("react/jsx-runtime").JSX.Element;
export {};
