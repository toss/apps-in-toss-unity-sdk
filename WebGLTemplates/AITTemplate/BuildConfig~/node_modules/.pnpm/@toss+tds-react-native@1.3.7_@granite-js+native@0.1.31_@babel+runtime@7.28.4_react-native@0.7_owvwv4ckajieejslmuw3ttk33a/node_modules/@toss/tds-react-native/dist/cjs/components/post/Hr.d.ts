import type { ViewProps } from 'react-native';
export interface PostHrProps {
    /**
     * 컴포넌트 하단의 여백을 결정해요.
     */
    paddingBottom?: number;
    /**
     * 컴포넌트의 스타일을 변경할 때 사용해요.
     */
    style?: ViewProps['style'];
}
export declare function PostHr({ paddingBottom, style }: PostHrProps): import("react/jsx-runtime").JSX.Element;
