import type { ViewProps } from 'react-native';
import { View } from 'react-native';
type Size = 'medium' | 'large' | 'big';
export interface EditableRatingProps extends ViewProps {
    /**
     * `Rating` 컴포넌트의 현재 점수를 결정해요.
     */
    value: number;
    /**
     * `Rating` 컴포넌트의 점수 상태가 바뀔 때 실행되는 함수에요.
     * @default undefined
     */
    onValueChange?: (value: number) => void;
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
     * `Rating` 컴포넌트를 클릭하거나 드래그할 때, 선택된 아이콘의 색상이 activeColor로 변경돼요.
     * 활성 상태에서는 지정한 값으로 표현돼요.
     * 비활성 상태에서는 `adaptive.greyOpacity200`로 표현돼요.
     *
     * @default adaptive.yellow400
     */
    activeColor?: string;
    /**
     * 이 값이 `true` 일 때 `Rating` 컴포넌트가 비활성화돼요.
     * @default false
     */
    disabled?: boolean;
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
export declare const EditableRating: import("react").ForwardRefExoticComponent<EditableRatingProps & import("react").RefAttributes<View>>;
export {};
