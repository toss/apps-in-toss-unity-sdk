import type { ComponentProps, ReactNode } from 'react';
import type { AccessibilityProps, StyleProp, ViewStyle } from 'react-native';
import { TouchableHighlight } from 'react-native';
export interface ListRowRef {
    blink: (duration?: number) => void;
    shine: (playCount?: number) => void;
}
export interface ListRowProps extends AccessibilityProps {
    left?: ReactNode;
    contents?: ReactNode;
    right?: ReactNode;
    withArrow?: boolean;
    leftAlignment?: 'top' | 'center';
    rightAlignment?: 'top' | 'center';
    /** ListRow 의 paddingHorizontal 을 제거하고 싶은경우 0을 줍니다. */
    horizontalPadding?: 0;
    verticalPadding?: 'extraSmall' | 8 | 'small' | 16 | 'medium' | 24 | 'large' | 32;
    containerStyle?: StyleProp<ViewStyle>;
    style?: StyleProp<ViewStyle>;
    onPress?: ComponentProps<typeof TouchableHighlight>['onPress'];
    preferReducedMotion?: boolean;
    disabledStyle?: 'type1' | 'type2';
    disabled?: boolean;
}
export declare const ListRow: import("react").ForwardRefExoticComponent<ListRowProps & import("react").RefAttributes<ListRowRef>>;
