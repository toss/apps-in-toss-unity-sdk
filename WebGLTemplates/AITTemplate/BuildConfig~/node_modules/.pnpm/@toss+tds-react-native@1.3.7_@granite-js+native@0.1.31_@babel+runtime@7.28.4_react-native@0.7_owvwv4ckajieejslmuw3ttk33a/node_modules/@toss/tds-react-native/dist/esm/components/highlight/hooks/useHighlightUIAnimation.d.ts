export declare const useHighlightUIAnimation: () => {
    style: {
        overlayOpacity: import("react-native").Animated.Value;
        holeStep: import("react-native").Animated.Value;
        messageOpacity: import("react-native").Animated.Value;
    };
    enter: () => Promise<void>;
    exit: () => Promise<void>;
};
