export type AnimationController = {
    start: () => Promise<boolean>;
    stop: () => void;
    reset: () => void;
    seekProgress: (offset: number) => void;
};
