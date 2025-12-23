import { Txt } from '../txt';
import type { ComponentProps } from 'react';
interface TableRowTextProps extends ComponentProps<typeof Txt> {
}
export declare function TableRowLeftText({ typography, color, ...props }: TableRowTextProps): import("react/jsx-runtime").JSX.Element;
export declare function TableRowRightText({ typography, color, style, ...props }: TableRowTextProps): import("react/jsx-runtime").JSX.Element;
export {};
