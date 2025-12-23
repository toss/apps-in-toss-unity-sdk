import type { ReactNode } from 'react';
import { DropdownItem } from './DropdownItem';
interface Props {
    title?: string;
    children?: ReactNode;
}
export declare function Dropdown({ title, children }: Props): import("react/jsx-runtime").JSX.Element;
export declare namespace Dropdown {
    var Item: typeof DropdownItem;
}
export {};
