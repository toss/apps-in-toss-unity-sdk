import type { SvgProps } from '@granite-js/native/react-native-svg';
import { Svg } from '@granite-js/native/react-native-svg';
import type { GetLinearGradientOption } from '@toss/tds-color-utils';
export type LinearGradientProps = GetLinearGradientOption & SvgProps;
export declare const LinearGradient: import("react").ForwardRefExoticComponent<LinearGradientProps & import("react").RefAttributes<Svg>>;
