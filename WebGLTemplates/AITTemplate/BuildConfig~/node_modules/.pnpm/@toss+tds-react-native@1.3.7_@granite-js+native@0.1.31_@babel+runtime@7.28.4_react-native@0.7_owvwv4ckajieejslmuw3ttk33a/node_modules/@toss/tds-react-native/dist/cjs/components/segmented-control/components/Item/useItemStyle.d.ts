import type { ViewProps } from 'react-native';
import type { ItemProps } from './Item';
interface ItemStyles {
    containerStyle: ViewProps['style'];
    labelStyle: ViewProps['style'];
    labelTypography: 't6' | 't5';
}
export declare function useItemStyle(props: Omit<ItemProps, 'value'>): ItemStyles;
export {};
