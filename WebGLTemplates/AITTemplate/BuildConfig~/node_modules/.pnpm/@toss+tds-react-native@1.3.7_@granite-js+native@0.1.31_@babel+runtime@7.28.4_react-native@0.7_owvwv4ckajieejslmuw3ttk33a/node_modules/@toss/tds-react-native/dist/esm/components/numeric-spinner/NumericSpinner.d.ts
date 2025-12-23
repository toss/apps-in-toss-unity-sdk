import type { View, ViewProps } from 'react-native';
export interface NumericSpinnerProps extends ViewProps {
    /**
     * `NumericSpinner` 컴포넌트의 크기에요
     */
    size: 'tiny' | 'small' | 'medium' | 'large';
    /**
     * `NumericSpinner` 컴포넌트에 표시되는 값이에요. 주로 입력값을 컴포넌트 외부에서 관리할 때 `onNumberChange` 속성과 함께 사용해요.
     *
     * @default 0
     */
    number?: number;
    /**
     * 입력할 수 있는 최소값이에요. 설정된 값보다 작은 값은 사용자가 입력할 수 없어요.
     *
     * @default 0
     */
    minNumber?: number;
    /**
     * 입력할 수 있는 최대값이에요. 설정된 값보다 큰 값은 사용자가 입력할 수 없어요.
     *
     * @default 999
     */
    maxNumber?: number;
    /**
     * 이 값이 true일 때 `NumericSpinner` 컴포넌트가 비활성화돼요. 사용자가 버튼을 눌러도 숫자가 변하지 않아요.
     *
     * @default false
     */
    disable?: boolean;
    /**
     * 입력값이 변경될 때 호출되는 함수예요. 변경된 숫자 값을 매개변수로 받아 처리해요.
     * 예를 들어, 입력값이 변경되면 이를 외부 상태에 반영할 때 사용해요.
     */
    onNumberChange?: (number: number) => void;
}
export declare const NumericSpinner: import("react").ForwardRefExoticComponent<NumericSpinnerProps & import("react").RefAttributes<View>>;
