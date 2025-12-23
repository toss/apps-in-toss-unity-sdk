import type { ViewStyle } from 'react-native';
import { Animated } from 'react-native';
interface Props {
    /**
     * @description 모션 방향
     */
    direction: 'x' | 'y';
    /**
     * @description 모션 크기
     */
    type: 'small' | 'big';
}
export declare function useWiggleAnimation({ direction, type }: Props): {
    startWiggle: () => void;
    style: Animated.WithAnimatedObject<ViewStyle>;
};
export {};
