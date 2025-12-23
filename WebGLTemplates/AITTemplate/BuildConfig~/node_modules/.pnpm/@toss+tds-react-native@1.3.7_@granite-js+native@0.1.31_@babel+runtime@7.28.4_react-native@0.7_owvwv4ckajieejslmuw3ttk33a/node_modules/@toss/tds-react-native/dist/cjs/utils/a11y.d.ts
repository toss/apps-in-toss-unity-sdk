import type { AccessibilityProps, AccessibilityRole, AccessibilityState, AccessibilityValue } from 'react-native';
interface A11yProps {
    role?: AccessibilityRole;
    label?: string;
    checked?: AccessibilityState['checked'];
    busy?: AccessibilityState['busy'];
    disabled?: AccessibilityState['disabled'];
    expanded?: AccessibilityState['expanded'];
    selected?: AccessibilityState['selected'];
    hidden?: boolean;
    valueMax?: AccessibilityValue['max'];
    valueMin?: AccessibilityValue['min'];
    valueNow?: AccessibilityValue['now'];
    valueText?: AccessibilityValue['text'];
}
/**
 * react-native에서 role과 accessibilityRole와 같이 차이가 발생하는 속성을 모두 지원해주기 위한 훅입니다.
 * @param restProps 사용처에서 넘겨주는 접근성 속성을 포함한 나머지 속성들
 * @param defaultProps 기본 접근성 속성
 * @returns T
 */
export declare function generateA11yRestProps<T extends AccessibilityProps>(restProps: T, defaultProps: A11yProps): T & AccessibilityProps;
export {};
