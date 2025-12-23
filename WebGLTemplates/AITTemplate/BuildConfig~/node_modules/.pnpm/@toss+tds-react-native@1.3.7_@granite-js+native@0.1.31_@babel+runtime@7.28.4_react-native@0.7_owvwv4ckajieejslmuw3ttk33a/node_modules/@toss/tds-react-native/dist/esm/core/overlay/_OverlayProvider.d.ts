import type { PropsWithChildren, ReactNode } from 'react';
import React from 'react';
export declare const OverlayContext: React.Context<{
    mount(id: string, element: ReactNode): void;
    unmount(id: string): void;
} | null>;
export declare function OverlayProvider({ children }: PropsWithChildren<{
    containerId?: string;
}>): import("react/jsx-runtime").JSX.Element;
