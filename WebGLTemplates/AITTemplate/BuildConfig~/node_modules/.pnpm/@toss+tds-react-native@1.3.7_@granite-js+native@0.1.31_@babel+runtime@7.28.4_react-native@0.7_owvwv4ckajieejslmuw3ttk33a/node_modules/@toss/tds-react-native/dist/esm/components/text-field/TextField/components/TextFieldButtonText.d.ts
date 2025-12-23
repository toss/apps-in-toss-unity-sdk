import { Txt } from '../../../txt';
import type { ComponentProps } from 'react';
type TxtProps = ComponentProps<typeof Txt>;
type Props = TxtProps & {
    placeholder?: string;
    placeholderColor?: string;
    children: string;
};
export declare function TextFieldButtonText({ color, placeholder, placeholderColor, children, ...props }: Props): import("react/jsx-runtime").JSX.Element;
export {};
