import type { ReactElement } from 'react';
import type { StyleProp, ViewStyle } from 'react-native';
import type { AssetUnion, LegacyFrameShape } from './types';
export interface AssetProps {
    resource: ReactElement;
    frame: LegacyFrameShape;
    union?: AssetUnion;
    style?: StyleProp<ViewStyle>;
}
/**
 * @description <Asset.Frame /> 을 사용해주세요.
 */
export declare const Asset: ({ resource, frame, union, style }: AssetProps) => import("react/jsx-runtime").JSX.Element;
