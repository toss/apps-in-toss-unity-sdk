import type { ReactNode } from 'react';
import type { PressableProps, StyleProp, ViewStyle } from 'react-native';
import { ListHeaderDescriptionParagraph } from './ListHeaderDescriptionParagraph';
import { ListHeaderRightArrow, ListHeaderRightText } from './ListHeaderRight';
import { ListHeaderTitleParagraph } from './ListHeaderTitleParagraph';
import { ListHeaderTitleSelector } from './ListHeaderTitleSelector';
import { ListHeaderTitleTextButton } from './ListHeaderTitleTextButton';
export interface ListHeaderRootProps {
    /**
     * `ListHeader`의 상단에 표시될 보조 설명 영역이에요.
     * `ListHeader.Description` 컴포넌트를 사용해서 보조 설명을 추가할 수 있어요.
     *
     * @example
     * ```tsx
     * <ListHeader.Description>보조 설명</ListHeader.Description>
     * ```
     */
    upper?: ReactNode;
    /**
     * `ListHeader`의 제목을 설정해요.
     * 제목 영역에는 주로 `ListHeader.TitleParagraph`, `ListHeader.TitleSelector`, `ListHeader.TitleTextButton` 컴포넌트가 사용돼요.
     *
     * @example
     * ```tsx
     * <ListHeader.TitleParagraph typography="t4">
     *   타이틀
     * </ListHeader.TitleParagraph>
     * ```
     */
    title: ReactNode;
    /**
     * 제목 영역의 스타일을 설정해요.
     */
    titleViewStyle?: StyleProp<ViewStyle>;
    /**
     * `ListHeader`의 오른쪽에 표시될 요소를 설정해요.
     * 오른쪽 영역에는 주로 `ListHeader.RightArrow` 또는 `ListHeader.RightText` 컴포넌트가 사용돼요.
     *
     * @example
     * ```tsx
     * <ListHeader.RightArrow typography="t6" color={adaptive.grey600}>
     *   오른쪽
     * </ListHeader.RightArrow>
     * ```
     */
    right?: ReactNode;
    /**
     * 오른쪽 영역의 스타일을 설정해요.
     */
    rightViewStyle?: StyleProp<ViewStyle>;
    /**
     * `ListHeader`의 하단에 표시될 보조 설명 영역이에요.
     * `ListHeader.Description` 컴포넌트를 사용해서 보조 설명을 추가할 수 있어요.
     *
     * @example
     * ```tsx
     * <ListHeader.Description>보조 설명</ListHeader.Description>
     * ```
     */
    lower?: ReactNode;
    /**
     * `ListHeader` 컴포넌트를 눌렀을 때 실행되는 함수예요.
     */
    onPress?: PressableProps['onPress'];
}
/**
 *
 * @example
 * <ListHeader
 *   upper={
 *     <ListHeader.Description>보조설명</ListHeader.Description>
 *   }
 *   title={
 *     <ListHeader.TitleParagraph typography="t4">
 *       타이틀
 *     </ListHeader.TitleParagraph>
 *   }
 *   right={
 *     <ListHeader.RightArrow typography="t6" color={adaptive.grey600}>
 *       오른쪽
 *     </ListHeader.RightArrow>
 *   }
 * />
 */
declare const _ListHeaderRoot: ({ upper, title, right, lower, onPress, rightViewStyle, titleViewStyle, }: ListHeaderRootProps) => import("react/jsx-runtime").JSX.Element;
type ListHeaderRootType = typeof _ListHeaderRoot & {
    Root: typeof _ListHeaderRoot;
    TitleParagraph: typeof ListHeaderTitleParagraph;
    TitleSelector: typeof ListHeaderTitleSelector;
    TitleTextButton: typeof ListHeaderTitleTextButton;
    DescriptionParagraph: typeof ListHeaderDescriptionParagraph;
    RightText: typeof ListHeaderRightText;
    RightArrow: typeof ListHeaderRightArrow;
};
declare const ListHeaderRoot: ListHeaderRootType;
export { ListHeaderRoot };
