import type { ComponentProps, ReactElement } from 'react';
import type { PressableProps } from 'react-native';
import { Animated } from 'react-native';
import type { TxtProps } from '../txt';
declare function DialogOverlay({ color, style, onPress, }: {
    color?: string;
    style?: ComponentProps<typeof Animated.View>['style'];
    onPress?: PressableProps['onPress'];
}): import("react/jsx-runtime").JSX.Element;
declare function DialogContent({ header, body, footer, }: {
    header?: ReactElement;
    body?: ReactElement;
    footer?: ReactElement;
}): import("react/jsx-runtime").JSX.Element;
declare function DialogHeaderTxt({ ...props }: TxtProps): import("react/jsx-runtime").JSX.Element;
declare function DialogBodyTxt({ ...props }: TxtProps): import("react/jsx-runtime").JSX.Element;
export { DialogBodyTxt, DialogContent, DialogHeaderTxt, DialogOverlay };
