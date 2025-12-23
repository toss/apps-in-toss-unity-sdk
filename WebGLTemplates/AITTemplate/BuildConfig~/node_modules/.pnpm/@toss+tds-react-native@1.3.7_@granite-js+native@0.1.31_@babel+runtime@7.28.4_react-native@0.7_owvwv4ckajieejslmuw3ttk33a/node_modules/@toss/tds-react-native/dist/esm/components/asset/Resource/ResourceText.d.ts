import type { TxtProps } from '../../txt/index';
type Size = 24 | 19 | 17 | 15 | 13 | 11;
interface ResourceTextProps extends TxtProps {
    size: Size;
}
export declare function ResourceText({ children, color, size, style, ...restProps }: ResourceTextProps): import("react/jsx-runtime").JSX.Element;
export {};
