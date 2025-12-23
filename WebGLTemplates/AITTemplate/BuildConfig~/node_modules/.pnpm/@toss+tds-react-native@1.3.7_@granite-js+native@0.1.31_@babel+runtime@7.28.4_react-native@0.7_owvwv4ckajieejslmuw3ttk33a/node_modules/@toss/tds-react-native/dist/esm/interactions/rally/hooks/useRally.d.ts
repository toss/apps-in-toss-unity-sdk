import type { Animated, LayoutChangeEvent, ViewStyle } from 'react-native';
import type { RallyProps } from '../types';
type Props = RallyProps & {
    /** render 가 되자마자 재생할 지 여부 */
    playOnRender?: boolean;
    /**
     * @description 정확한 초기 레이아웃 계산을 위해, mounted 되기 전에 opacity, transform 초기화 style을 적용할지 여부
     * @default true
     * */
    applyBeforeMountedStyle?: boolean;
};
export declare function useRally({ playOnRender, exit, motion: _motion, defaultDuration, defaultEasing, applyBeforeMountedStyle, ...playConfigs }: Props): {
    onLayout: (event: LayoutChangeEvent) => void;
    style: Animated.WithAnimatedObject<ViewStyle> | undefined;
    startForward: () => Promise<boolean>;
    startBackward: () => Promise<boolean>;
    play: () => Promise<boolean>;
    seekProgress: (offset: number) => void;
    reset: () => void;
    pause: () => void;
};
export {};
