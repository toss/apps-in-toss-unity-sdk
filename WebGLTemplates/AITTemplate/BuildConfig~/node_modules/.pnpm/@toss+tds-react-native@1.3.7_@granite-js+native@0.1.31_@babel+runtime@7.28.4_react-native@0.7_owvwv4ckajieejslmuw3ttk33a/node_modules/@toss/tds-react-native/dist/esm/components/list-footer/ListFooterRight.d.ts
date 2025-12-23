import type { ReactNode } from 'react';
import type { StyleProp, ViewStyle } from 'react-native';
export interface ListFooterRightProps {
    /**
     * `ListFooter.Right` 컴포넌트의 스타일을 변경할 때 사용해요.
     */
    style?: StyleProp<ViewStyle>;
    /**
     * `ListFooter.Right` 컴포넌트 내부에 표시될 내용을 지정해요.
     */
    children: ReactNode;
}
export declare function ListFooterRight({ children, style }: ListFooterRightProps): import("react/jsx-runtime").JSX.Element;
