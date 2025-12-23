import type { TypographyKeys } from '../../txt';
import type { ReactNode } from 'react';
type ListType = 'ol' | 'ul';
export type PostList = {
    listType: ListType;
    typography?: TypographyKeys;
};
interface Props {
    listType: ListType;
    typography?: TypographyKeys;
    children?: ReactNode;
}
export declare function PostListProvider({ listType, typography, children }: Props): import("react/jsx-runtime").JSX.Element;
export declare function usePostList(): PostList;
export {};
