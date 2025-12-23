import type { PropsWithChildren } from 'react';
export type ErrorPageProps = {
    statusCode?: number;
    title?: string;
    subtitle?: string;
    onPressRightButton?: () => void;
    onPressLeftButton?: () => void;
} & PropsWithChildren;
export declare const ErrorPage: ({ statusCode, title, subtitle, onPressLeftButton, onPressRightButton, children, }: ErrorPageProps) => import("react/jsx-runtime").JSX.Element;
