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
export declare function useWiggleAnim({ direction, type }: Props): {
    startWiggle: () => void;
    style: {
        transform: ({
            translateX: Animated.Value;
            translateY?: undefined;
        } | {
            translateY: Animated.Value;
            translateX?: undefined;
        })[];
    };
};
export {};
