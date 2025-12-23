import type { TxtProps } from '../txt';
import type { Indent } from './types';
interface AgreementDescriptionProps extends Pick<TxtProps, 'children' | 'style'> {
    indent: Indent;
}
export declare function AgreementDescription({ children, indent, style, ...props }: AgreementDescriptionProps): import("react/jsx-runtime").JSX.Element;
export {};
