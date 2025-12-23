import type { Animated as ReactNativeAnimated } from 'react-native';
import type { TimeControl, Transition } from '../types';
export declare function createMotionPropertyAnimation({ progress, toValue, useNativeDriver, delay, ...playConfig }: {
    progress: ReactNativeAnimated.Value;
    useNativeDriver: boolean;
    toValue: number;
} & Transition & TimeControl): ReactNativeAnimated.CompositeAnimation;
