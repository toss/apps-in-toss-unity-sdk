import type { IconProps } from '../../icon';
import type { AssetResourceScale, AssetResourceScaleType } from '../types';
interface IconResourceProps extends Pick<IconProps, 'name' | 'color' | 'accessibilityLabel' | 'style'> {
    scale?: AssetResourceScale;
    scaleType?: AssetResourceScaleType;
}
export declare function ContentIcon({ name, color, scale, accessibilityLabel, scaleType, style, }: IconResourceProps): import("react/jsx-runtime").JSX.Element;
export {};
