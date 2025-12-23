import type { SvgProps } from '@granite-js/native/react-native-svg';
import { Svg } from '@granite-js/native/react-native-svg';
type ColorWithOpacity = {
    value: string;
    opacity?: number;
};
type Option = {
    colors: Array<ColorWithOpacity | string>;
    /**
     * @description 색깔 분포. 색상 배열과 동일한 길이.
     * [0, 1]
     */
    positions?: number[];
};
export type RadialGradientProps = Option & SvgProps;
export declare const RadialGradient: import("react").ForwardRefExoticComponent<Option & SvgProps & import("react").RefAttributes<Svg>>;
export {};
