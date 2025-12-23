import type { ComponentProps, ReactNode } from 'react';
import { View } from 'react-native';
interface Props extends Omit<ComponentProps<typeof View>, 'children'> {
    content: ReactNode;
}
export declare function UpperAssetContent({ content, style, ...restProps }: Props): import("react/jsx-runtime").JSX.Element;
export {};
