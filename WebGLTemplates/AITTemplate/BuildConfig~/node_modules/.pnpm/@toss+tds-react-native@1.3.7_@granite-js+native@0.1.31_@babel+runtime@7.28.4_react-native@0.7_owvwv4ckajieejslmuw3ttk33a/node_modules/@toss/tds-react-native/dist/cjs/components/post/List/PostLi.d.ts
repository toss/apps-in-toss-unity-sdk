import type { ReactNode } from 'react';
import type { ViewProps } from 'react-native';
export interface PostLiProps {
    /**
     * 컴포넌트 내부에 표시될 내용을 지정해요.
     */
    children: ReactNode;
    /**
     * 텍스트 색상을 지정해요.
     */
    color?: string;
    /**
     * 컴포넌트의 스타일을 변경할 때 사용해요.
     */
    style?: ViewProps['style'];
}
export declare function PostLi(props: PostLiProps): import("react/jsx-runtime").JSX.Element;
export declare namespace PostLi {
    var displayName: string;
}
