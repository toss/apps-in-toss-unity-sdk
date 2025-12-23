import type { ViewProps } from 'react-native';
import { View } from 'react-native';
export interface SliderProps extends ViewProps {
    min?: number;
    max?: number;
    value?: number;
    /**
     * 간격
     * @note 양의 정수만 입력 가능합니다.
     */
    step?: number;
    onChange?: (value: number) => void;
    color?: string;
}
export declare const Slider: import("react").ForwardRefExoticComponent<SliderProps & import("react").RefAttributes<View>>;
