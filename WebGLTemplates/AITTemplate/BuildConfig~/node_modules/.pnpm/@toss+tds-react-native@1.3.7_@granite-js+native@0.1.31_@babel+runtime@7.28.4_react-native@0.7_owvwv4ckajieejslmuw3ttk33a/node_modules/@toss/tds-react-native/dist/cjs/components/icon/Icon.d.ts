import type { AccessibilityProps, ViewProps } from 'react-native';
import { View } from 'react-native';
export interface IconProps extends Pick<ViewProps, 'style'>, AccessibilityProps {
    size?: number;
    color?: string;
    /** @default default */
    type?: 'default' | 'circle';
    name: string;
}
declare const _default: import("react").ForwardRefExoticComponent<IconProps & import("react").RefAttributes<View>> & {
    prefetchIcon: (name: string) => Promise<void>;
};
export default _default;
