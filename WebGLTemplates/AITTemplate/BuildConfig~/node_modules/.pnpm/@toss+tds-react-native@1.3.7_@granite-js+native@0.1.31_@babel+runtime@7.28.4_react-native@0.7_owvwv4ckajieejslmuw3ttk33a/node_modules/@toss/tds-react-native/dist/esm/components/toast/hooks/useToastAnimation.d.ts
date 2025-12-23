type UseToastAnimationProps = {
    position: 'top' | 'bottom';
    initialY?: number;
};
export declare const useToastAnimation: ({ position, initialY }: UseToastAnimationProps) => {
    enter: () => Promise<void>;
    exit: () => Promise<void>;
    animatedStyle: {
        opacity: import("react-native").Animated.Value;
        transform: {
            translateY: import("react-native").Animated.Value;
        }[];
    };
};
export {};
