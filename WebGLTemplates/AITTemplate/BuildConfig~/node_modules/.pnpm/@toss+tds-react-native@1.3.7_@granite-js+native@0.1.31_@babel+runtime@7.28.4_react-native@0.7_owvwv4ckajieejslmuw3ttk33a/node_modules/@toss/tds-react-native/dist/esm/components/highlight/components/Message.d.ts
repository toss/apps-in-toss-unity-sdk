import type { Layout, XAlignmentValue, YAlignmentValue } from '../types';
type MessageProps = {
    color: string;
    children: string;
    layout: Layout;
    xAlignment: XAlignmentValue;
    yAlignment: YAlignmentValue;
};
export declare const Message: ({ color, children, layout, xAlignment, yAlignment }: MessageProps) => import("react/jsx-runtime").JSX.Element;
export {};
