import { SafeAreaView } from '@granite-js/native/react-native-safe-area-context';
import type { ComponentProps } from 'react';
export declare const SafeArea: {
    Top: typeof SafeAreaTop;
    Right: typeof SafeAreaRight;
    Bottom: typeof SafeAreaBottom;
    Left: typeof SafeAreaLeft;
};
type SafeAreaProps = Omit<ComponentProps<typeof SafeAreaView>, 'children'>;
declare function SafeAreaTop({ edges, ...props }: SafeAreaProps): import("react/jsx-runtime").JSX.Element;
declare function SafeAreaRight({ edges, ...props }: SafeAreaProps): import("react/jsx-runtime").JSX.Element;
declare function SafeAreaBottom({ edges, ...props }: SafeAreaProps): import("react/jsx-runtime").JSX.Element;
declare function SafeAreaLeft({ edges, ...props }: SafeAreaProps): import("react/jsx-runtime").JSX.Element;
export {};
