import type { ViewProps } from 'react-native';
import { View } from 'react-native';
export interface ProgressIconsProps extends ViewProps {
    max: number;
    name: string;
    size: number;
    gap: number;
    value: number;
    activeColor: string;
    idleColor: string;
}
export declare const ProgressIcons: import("react").ForwardRefExoticComponent<ProgressIconsProps & import("react").RefAttributes<View>>;
type Size = 'tiny' | 'small' | 'medium' | 'large' | 'big';
export interface ReadOnlyRatingProps extends ViewProps {
    /**
     * `Rating` 컴포넌트의 현재 점수를 결정해요.
     */
    value: number;
    /**
     * `Rating` 컴포넌트의 형태를 결정해요.
     * - `full`: 전체 아이콘과 점수가 함께 보여져요.
     * - `compact`: 하나의 아이콘과 점수가 함께 보여져요.
     * - `iconOnly`: 전체 아이콘만 보여져요.
     */
    variant: 'full' | 'compact' | 'iconOnly';
    /**
     * `Rating` 컴포넌트의 크기를 결정해요.
     */
    size: Size;
    /**
     * `Rating` 컴포넌트에 지정 가능한 최대 점수를 결정해요.
     * @default 5
     */
    max?: number;
    /**
     * `Rating` 컴포넌트의 요소 간의 간격을 결정해요.
     * 지정하지 않으면 `size`에 따라 미리 정의된 값이 할당돼요.
     */
    gap?: number;
    /**
     * `Rating` 컴포넌트의 활성 색상을 지정해요.
     * 비활성 상태에서는 `adaptive.greyOpacity200`로 표현돼요.
     *
     * @default adaptive.yellow400
     */
    activeColor?: string;
    /**
     * 역할을 나타내요.
     * @example
     * <Rating accessibilityLabel="별점 평가" />
     */
    accessibilityLabel?: string;
    /**
     * 최대 몇점 중 현재 몇점인지 나타내요.
     * @example
     * <Rating accessibilityValueText={`${max}점 만점 중 ${value}점`} />
     */
    accessibilityValueText?: string | ((max: number, value: number) => string);
}
export declare const ReadOnlyRating: import("react").ForwardRefExoticComponent<ReadOnlyRatingProps & import("react").RefAttributes<View>>;
export {};
