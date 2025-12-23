import type { TxtProps } from '../txt';
type LeftTextProps = TxtProps & {
    children: string;
};
declare function ListRowLeftText({ style, ...props }: LeftTextProps): import("react/jsx-runtime").JSX.Element;
export { ListRowLeftText };
