import type { ReactNode } from 'react';
import type { PressableProps } from 'react-native';
export interface AmountTopProps {
    topPadding?: 0 | 64 | 80;
    bottomPadding?: 0 | 16 | 24;
    horizontalPadding?: 0 | 24;
    subTitle: ReactNode;
    title: ReactNode;
    button?: ReactNode;
}
declare function AmountTop({ topPadding, bottomPadding, horizontalPadding, subTitle, title, button: originalButton, }: AmountTopProps): import("react/jsx-runtime").JSX.Element;
declare namespace AmountTop {
    var SubTitle: (props: {
        children: string;
        onPress?: PressableProps["onPress"];
        underline?: boolean;
    }) => import("react/jsx-runtime").JSX.Element;
}
export default AmountTop;
