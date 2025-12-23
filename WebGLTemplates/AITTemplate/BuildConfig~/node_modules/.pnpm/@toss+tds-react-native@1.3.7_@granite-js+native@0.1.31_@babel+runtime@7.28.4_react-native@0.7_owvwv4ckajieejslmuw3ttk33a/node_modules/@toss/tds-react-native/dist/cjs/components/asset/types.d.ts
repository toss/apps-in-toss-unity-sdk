import type { DimensionValue, ViewProps } from 'react-native';
/**
 * AssetFrame
 */
export type FrameOverlapShape = {
    x?: number;
    y?: number;
    blur?: number;
};
export type LegacyFrameShape = {
    width?: number;
    height?: number;
    radius?: number;
    color?: string;
    overlap?: FrameOverlapShape;
};
/**
 * AssetResource
 */
export type AssetResourceScale = number;
export type AssetResourceScaleType = 'crop' | 'fit';
/**
 * AssetUnion
 */
interface AssetOverlapUnion {
    type: 'overlap';
    color: string;
}
export type AssetUnion = AssetOverlapUnion;
export type ScaleType = 'fit' | 'crop';
export type FrameAccShapeType = {
    width?: DimensionValue;
    height?: DimensionValue;
    x?: DimensionValue;
    y?: DimensionValue;
};
export type FrameOverlapShapeType = {
    x?: number;
    y?: number;
    blur?: number;
    spread?: string | number;
};
export type FrameShapeType = {
    width?: number;
    height?: number;
    radius?: number;
    acc?: FrameAccShapeType;
    overlap?: FrameOverlapShapeType;
};
export type FrameShapeKeys = 'SquareLarge' | 'SquareMedium' | 'SquareSmall' | 'RectangleLarge' | 'RectangleMedium' | 'CardLarge' | 'CardMedium' | 'CardSmall' | 'CircleLarge' | 'CircleMedium' | 'CircleSmall' | 'CleanH20' | 'CleanH24' | 'CleanH28' | 'CleanW20' | 'CleanW24' | 'CleanW32' | 'CleanW40' | 'CleanW60' | 'CleanW80' | 'CleanW120' | 'CleanW200';
export type AssetCommonType = {
    frameShape?: FrameShapeType;
    backgroundColor?: string;
    style?: ViewProps['style'];
    acc?: React.ReactNode;
    accPosition?: FrameAccPositionType;
    overlap?: {
        color: string;
    } | null;
};
export type FrameAccPositionType = 'top-left' | 'top-right' | 'bottom-left' | 'bottom-right';
export {};
