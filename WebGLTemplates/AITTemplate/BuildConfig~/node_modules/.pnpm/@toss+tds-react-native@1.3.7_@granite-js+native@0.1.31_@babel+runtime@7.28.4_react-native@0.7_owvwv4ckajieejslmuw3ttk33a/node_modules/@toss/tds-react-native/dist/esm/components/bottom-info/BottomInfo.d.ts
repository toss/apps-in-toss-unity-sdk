import type { ReactNode } from 'react';
import type { StyleProp, ViewStyle } from 'react-native';
interface BottomInfoProps {
    children?: ReactNode | ReactNode[];
    style?: StyleProp<ViewStyle>;
}
/**
 *
 * @example
 *
 * // with Post
 * <BottomInfo>
 *   <PostUl horizontalPadding={0}>
 *     <PostUl.Li size="small" indent={0} marginBottom={8}>
 *       리스트
 *     </PostUl.Li>
 *     <PostUl.Li size="small" indent={0} marginBottom={0}>
 *       리스트
 *     </PostUl.Li>
 *   </PostUl>
 * </BottomInfo>
 */
declare function BottomInfo({ children, style }: BottomInfoProps): import("react/jsx-runtime").JSX.Element;
export default BottomInfo;
