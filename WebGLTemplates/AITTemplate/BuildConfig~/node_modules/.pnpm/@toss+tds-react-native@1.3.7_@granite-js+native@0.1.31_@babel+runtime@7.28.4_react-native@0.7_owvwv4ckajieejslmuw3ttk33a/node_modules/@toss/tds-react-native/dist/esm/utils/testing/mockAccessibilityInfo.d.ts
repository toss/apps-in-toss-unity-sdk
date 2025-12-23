import { AccessibilityInfo } from 'react-native';
type EnableAccessibilityInfoKey = keyof Pick<AccessibilityInfo, 'isReduceMotionEnabled' | 'isScreenReaderEnabled'>;
export declare function mockAccessibilityInfo(accessibilityinfo: {
    [key in EnableAccessibilityInfoKey]?: boolean;
}): void;
export {};
