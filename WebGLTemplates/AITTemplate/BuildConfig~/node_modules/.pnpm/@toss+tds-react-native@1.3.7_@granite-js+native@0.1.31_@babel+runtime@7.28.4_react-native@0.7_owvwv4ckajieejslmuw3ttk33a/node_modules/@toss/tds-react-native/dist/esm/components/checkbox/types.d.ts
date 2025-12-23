import type { PressableProps, StyleProp, ViewStyle } from 'react-native';
export interface CheckboxBaseProps extends PressableProps {
    /**
     * `Checkbox` 컴포넌트의 선택 상태를 설정해요.
     */
    checked: boolean;
    /**
     * `Checkbox` 컴포넌트의 크기를 설정해요.
     */
    size: number;
    /**
     * `Checkbox` 컴포넌트의 스타일을 설정해요.
     */
    style?: StyleProp<ViewStyle>;
}
export interface CheckboxProps extends Pick<CheckboxBaseProps, 'style' | 'children' | keyof PressableProps> {
    /**
     * 이 값이 `true`일 때 해당 `Checkbox`가 선택된 상태로 표현돼요. 주로 `Checkbox` 컴포넌트의 상태를 컴포넌트 외부에서 관리할 때, `onCheckedChange` 속성과 함께 사용해요.
     */
    checked?: boolean;
    /**
     * `Checkbox` 컴포넌트의 크기를 설정해요.
     *
     * @default 24
     */
    size?: number;
    /**
     * 이 값이 `true`일 때 `Checkbox` 컴포넌트가 비활성화돼요.
     */
    disabled?: boolean;
    /**
     * `Checkbox` 컴포넌트의 상태를 컴포넌트 내부에서 관리할 때, 초기 선택 상태를 지정해요.
     */
    defaultChecked?: boolean;
    /**
     * `Checkbox` 컴포넌트의 선택 상태가 변경될 때 실행되는 함수예요.
     */
    onCheckedChange?: (checked: boolean) => void;
}
