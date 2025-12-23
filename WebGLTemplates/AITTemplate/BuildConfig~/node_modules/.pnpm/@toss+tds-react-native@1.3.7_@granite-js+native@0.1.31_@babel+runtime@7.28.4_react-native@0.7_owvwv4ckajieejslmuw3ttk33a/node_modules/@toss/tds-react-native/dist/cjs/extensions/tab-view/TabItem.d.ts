import type { ReactNode } from 'react';
import type { PressableProps, StyleProp, ViewStyle } from 'react-native';
import type { TabValue } from './TabsContext';
interface TabItemProps {
    redBean?: boolean;
    value: TabValue;
    children: ReactNode;
    style?: StyleProp<ViewStyle>;
    tabIndex?: number;
    onPress?: PressableProps['onPress'];
}
declare function TabItem({ redBean, value, children, style, onPress, tabIndex }: TabItemProps): import("react/jsx-runtime").JSX.Element;
export { TabItem };
