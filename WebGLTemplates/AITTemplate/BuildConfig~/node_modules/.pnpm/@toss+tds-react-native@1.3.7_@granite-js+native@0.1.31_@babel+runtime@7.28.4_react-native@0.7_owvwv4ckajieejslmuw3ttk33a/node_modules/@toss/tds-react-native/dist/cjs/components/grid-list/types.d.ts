import type { ReactNode } from 'react';
import type { StyleProp, TouchableHighlightProps, ViewStyle } from 'react-native';
export type GridListColumn = 1 | 2 | 3;
export interface GridListProps {
    children: ReactNode | ReactNode[];
    column: 1 | 2 | 3;
    style?: StyleProp<ViewStyle>;
}
export interface GridListItemProps {
    /**
     * maxHeight: 28
     */
    image: ReactNode;
    title: ReactNode;
    style?: StyleProp<ViewStyle>;
    onPress?: TouchableHighlightProps['onPress'];
}
