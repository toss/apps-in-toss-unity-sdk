import type { ReactNode } from 'react';
import type { ViewProps } from 'react-native';
import type { TypographyKeys } from '../../txt';
type ListType = 'ol' | 'ul';
export interface PostListProps {
    typography?: TypographyKeys;
    children: ReactNode;
    style?: ViewProps['style'];
    paddingBottom?: number;
    listType: ListType;
}
export declare function PostList({ listType, typography, paddingBottom, style, children, }: PostListProps): import("react/jsx-runtime").JSX.Element;
export {};
