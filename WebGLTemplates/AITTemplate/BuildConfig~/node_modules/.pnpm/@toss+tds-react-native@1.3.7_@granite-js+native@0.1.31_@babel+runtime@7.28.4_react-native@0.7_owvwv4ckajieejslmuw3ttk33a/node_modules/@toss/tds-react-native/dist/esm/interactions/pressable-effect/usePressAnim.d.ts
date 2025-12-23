import { Animated } from 'react-native';
interface Props {
    pressIn: {
        scale: number;
        opacity: number;
    };
    pressOut: {
        scale: number;
        opacity: number;
    };
}
export declare function usePressAnim({ pressIn, pressOut }: Props): {
    scaleAnim: Animated.Value;
    opacityAnim: Animated.Value;
    startPressInAnim: () => void;
    startPressOutAnim: () => void;
};
export {};
