import type { ReactNode } from 'react';
import type { TouchableHighlightProps } from 'react-native';
import { ListFooterRight } from './ListFooterRight';
import { ListFooterTitle } from './ListFooterTitle';
export interface ListFooterProps {
    /**
     * `ListFooter` 컴포넌트의 제목을 설정해요.
     * 주로 `ListFooter.Title` 컴포넌트를 사용해요.
     */
    title: ReactNode;
    /**
     * `ListFooter` 컴포넌트의 오른쪽 영역에 표시할 요소를 설정해요.
     * 주로 `ListFooter.Right` 컴포넌트를 사용해요.
     */
    right?: ReactNode;
    /**
     * `ListFooter` 컴포넌트의 하단 구분선 스타일을 설정해요.
     * - `full`: 전체 너비로 표시해요.
     * - `none`: 구분선을 표시하지 않아요.
     *
     * @default 'full'
     */
    borderType?: 'full' | 'none';
    /**
     * `ListFooter` 컴포넌트를 눌렀을 때 실행되는 함수예요.
     */
    onPress?: TouchableHighlightProps['onPress'];
}
/**
 *
 * @example
 * <ListFooter
 *   borderType="full"
 *   title={<ListFooter.Title>더 보기</ListFooter.Title>}
 *   right={
 *     <ListFooter.Right>
 *       <Icon name="icon-plus" size={16} />
 *     </ListFooter.Right>
 *   }
 * />
 */
declare function ListFooter({ title, right, borderType, onPress }: ListFooterProps): import("react/jsx-runtime").JSX.Element;
declare namespace ListFooter {
    var Title: typeof ListFooterTitle;
    var Right: typeof ListFooterRight;
}
export default ListFooter;
