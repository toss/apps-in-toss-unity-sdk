import type { AccessibilityProps, PressableProps, StyleProp, ViewStyle } from 'react-native';
interface SwitchProp extends AccessibilityProps {
    /**
     * `Switch` 컴포넌트의 켜짐과 꺼짐 상태를 표현해요.  주로 `Switch` 컴포넌트의 상태를 컴포넌트 외부에서 관리할 때, `onCheckedChange` 속성과 함께 사용해요.
     */
    checked?: boolean;
    /**
     * `Switch` 컴포넌트의 선택 상태가 변경될 때 실행되는 함수예요.
     */
    onCheckedChange?: (value: boolean) => void;
    /**
     * `Switch` 컴포넌트의 상태를 컴포넌트 내부에서 관리할 때, 초기 선택 상태를 지정해요.
     *
     * @default false
     */
    defaultChecked?: boolean;
    /**
     * 이 값이 `true`일 때 컴포넌트가 비활성화돼요.
     *
     * @default false
     */
    disabled?: boolean;
    /**
     * `Switch` 컴포넌트를 클릭했을 때 실행되는 함수예요.
     */
    onPress?: PressableProps['onPress'];
    /**
     * `Switch` 컴포넌트의 스타일을 설정해요.
     */
    style?: StyleProp<ViewStyle>;
}
declare function Switch({ checked: _checked, defaultChecked, disabled, style, onPress, onCheckedChange, accessibilityState, ...restProps }: SwitchProp): import("react/jsx-runtime").JSX.Element;
export default Switch;
