import type { PropsWithChildren } from 'react';
import type { StyleProp, ViewStyle } from 'react-native';
export interface SkeletonBaseProps {
    width?: ViewStyle['width'];
    height?: ViewStyle['height'];
    borderRadius?: ViewStyle['borderRadius'];
    style?: StyleProp<ViewStyle>;
}
type SkeletonProps = PropsWithChildren<SkeletonBaseProps>;
declare function Skeleton({ width, height, borderRadius, style }: SkeletonProps): import("react/jsx-runtime").JSX.Element;
export default Skeleton;
