import type { ReactNode } from 'react';
type Props = {
    children: ReactNode;
    onPressIn: () => void;
    disabled?: boolean;
    color?: string;
};
export declare function ControlKeyButton({ children, onPressIn, color, disabled }: Props): import("react/jsx-runtime").JSX.Element;
export {};
