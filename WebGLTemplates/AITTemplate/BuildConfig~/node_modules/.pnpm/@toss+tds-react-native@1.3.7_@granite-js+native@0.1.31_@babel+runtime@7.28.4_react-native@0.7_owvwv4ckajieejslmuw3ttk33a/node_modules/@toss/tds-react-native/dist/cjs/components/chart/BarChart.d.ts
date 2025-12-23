import type { View, ViewProps } from 'react-native';
type Theme = 'blue' | 'green' | 'yellow' | 'orange' | 'red' | 'grey';
type SingleBarFill = {
    /**
     * `BarChart` 컴포넌트의 오른쪽 끝 하나만 색상을 채우고, 나머지는 기본색(`grey100`)으로 설정돼요. `color` 속성을 사용해서 색상을 설정할 수 있어요.
     */
    type: 'single-bar';
    /**
     * `BarChart` 컴포넌트에 채울 색상을 설정해요.
     */
    theme: Theme;
};
type AllBarFill = {
    /**
     * `BarChart` 컴포넌트를 하나의 색상으로 채워요. `color` 속성을 사용해서 색상을 설정할 수 있어요.
     */
    type: 'all-bar';
    /**
     * `BarChart` 컴포넌트에 채울 색상을 설정해요.
     */
    theme: Theme;
};
type AutoFill = {
    /**
     * `BarChart` 컴포넌트를 기본 규칙에 따라 막대의 색상을 오른쪽 부터 채워요. `count` 속성을 통해 색상이 적용될 막대의 개수를 제한할 수 있어요.
     *  색상 순서는 다음과 같아요: ['blue', 'green', 'yellow', 'orange', 'red', 'grey'].
     */
    type: 'auto';
    /**
     * 색상을 적용할 막대의 개수예요.
     */
    count: number;
};
export interface BarChartProps extends ViewProps {
    /**
     * `BarChart` 컴포넌트에 표시할 데이터를 설정해요.
     *
     * @default []
     *
     * @remarks
     * - `value`는 0 이상의 정수여야 해요.
     * - `xAxisLabel`은 x축에 표시할 라벨을 설정해요.
     * - `value`와 `xAxisLabel`은 1:1로 매칭돼야 해요.
     *
     * @example
     * ```typescript
     * const data = [
     *   { value: 10, xAxisLabel: 'Label1' },
     *   { value: 20, xAxisLabel: 'Label2' },
     * ];
     * ```
     */
    data?: Array<{
        value: number;
        xAxisLabel?: string;
    }>;
    /**
     * `BarChart` 컴포넌트의 색상 채우기 방식을 설정해요.
     *
     * @default 'all-bar' 모든 막대를 `blue` 색상으로 채워요.
     *
     * @remarks
     * - `single-bar`: 오른쪽 끝 하나의 막대만 색상을 채우고, 나머지는 기본색(`grey100`)으로 설정돼요. `color` 속성을 사용해서 색상을 설정할 수 있어요.
     * - `all-bar`: 모든 막대를 하나의 색상으로 채워요. `color` 속성을 사용해서 색상을 설정할 수 있어요.
     * - `auto`: 기본 규칙에 따라 막대의 색상을 오른쪽 부터 채워요. `count` 속성을 통해 색상이 적용될 막대의 개수를 제한할 수 있어요.
     *   색상 순서는 다음과 같아요: ['blue', 'green', 'yellow', 'orange', 'red', 'grey'].
     *
     * @example
     * ```typescript
     * fill={{ type: 'single-bar', theme: 'blue' }}
     * fill={{ type: 'all-bar', theme: 'green' }}
     * fill={{ type: 'auto', count: 3 }}
     * ```
     */
    fill?: SingleBarFill | AllBarFill | AutoFill;
    /**
     * `BarChart` 컴포넌트의 너비를 설정해요.
     *
     * @default Dimensions.get('window').width `window`의 너비를 기본값으로 사용해요.
     */
    width?: number;
    /**
     * `BarChart` 컴포넌트의 높이를 설정해요.
     *
     * @default 205
     */
    height?: number;
}
export declare const BarChart: import("react").ForwardRefExoticComponent<BarChartProps & import("react").RefAttributes<View>>;
export {};
