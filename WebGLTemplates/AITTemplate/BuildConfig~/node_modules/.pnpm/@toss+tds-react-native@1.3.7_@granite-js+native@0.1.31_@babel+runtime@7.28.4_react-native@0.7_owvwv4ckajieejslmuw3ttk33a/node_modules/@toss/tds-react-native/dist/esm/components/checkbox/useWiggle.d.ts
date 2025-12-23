import { Animated } from 'react-native';
export default function useWiggle(): {
    startWiggle: () => void;
    style: {
        transform: {
            translateX: Animated.Value;
        }[];
    };
};
