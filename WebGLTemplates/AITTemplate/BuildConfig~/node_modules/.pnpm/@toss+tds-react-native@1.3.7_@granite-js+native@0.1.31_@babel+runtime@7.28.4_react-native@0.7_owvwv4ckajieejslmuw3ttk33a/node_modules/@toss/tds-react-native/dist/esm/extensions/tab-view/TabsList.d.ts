import type { ReactNode } from 'react';
import type { ViewProps } from 'react-native';
interface Props extends ViewProps {
    children: ReactNode;
}
export declare function TabsList({ children, style, ...viewProps }: Props): import("react/jsx-runtime").JSX.Element;
export {};
