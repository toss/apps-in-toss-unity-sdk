import type { StyleProp, ViewStyle } from 'react-native';
import type { FontWeightKeys, TypographyKeys } from '../../txt';
type Type = 'blue' | 'teal' | 'green' | 'red' | 'yellow' | 'elephant';
type Style = 'fill' | 'weak';
export interface ParagraphBadgeProps {
    /**
     * `ParagraphBadge` 컴포넌트 내부에 표시될 텍스트를 지정해요.
     */
    children: string;
    /**
     * `ParagraphBadge` 컴포넌트의 왼쪽 여백을 설정해요.
     *
     * @default 0
     */
    marginLeft?: number;
    /**
     * `ParagraphBadge` 컴포넌트의 오른쪽 여백을 설정해요.
     *
     * @default 0
     */
    marginRight?: number;
    /**
     * `ParagraphBadge` 컴포넌트의 스타일(모양)을 설정해요. 'fill'은 채도가 높은 스타일, 'weak'은 채도가 낮은 스타일이에요.
     *
     * @default 'fill'
     */
    badgeStyle?: Style;
    /**
     * `ParagraphBadge` 컴포넌트의 색상을 설정해요.
     *
     * @default 'blue'
     */
    type?: Type;
    /**
     * `ParagraphBadge` 컴포넌트의 텍스트 스타일을 설정해요. 기본값은 상위 `Paragraph` 컴포넌트의 값을 따라가요.
     * @default 't5'
     */
    typography?: TypographyKeys;
    /**
     * `ParagraphBadge` 컴포넌트의 스타일을 변경할 때 사용해요.
     */
    style?: StyleProp<ViewStyle>;
    /**
     * `ParagraphBadge` 컴포넌트의 텍스트 굵기를 설정해요.
     *
     * @default 'semiBold'
     */
    fontWeight?: FontWeightKeys;
}
declare function ParagraphBadge({ children, type, badgeStyle, marginLeft, marginRight, typography: typographyKeyProp, style, fontWeight, }: ParagraphBadgeProps): import("react/jsx-runtime").JSX.Element;
export { ParagraphBadge };
