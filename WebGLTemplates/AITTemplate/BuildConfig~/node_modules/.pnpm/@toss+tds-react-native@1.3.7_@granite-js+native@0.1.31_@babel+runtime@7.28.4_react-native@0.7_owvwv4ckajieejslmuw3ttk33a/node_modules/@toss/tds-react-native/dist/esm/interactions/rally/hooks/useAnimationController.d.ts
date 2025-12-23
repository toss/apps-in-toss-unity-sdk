import type { AnimationController, RallyPlayConfig } from '../types';
type Props = {
    forwardAnimation: AnimationController;
    backwardAnimation: AnimationController;
    mounted: boolean;
} & RallyPlayConfig;
export declare function useAnimationController({ forwardAnimation, backwardAnimation, playCount: _playCount, loopDelay, loopMode, mounted, delay: initialDelay, }: Props): {
    startForward: () => Promise<boolean>;
    startBackward: () => Promise<boolean>;
    play: () => Promise<boolean>;
    seekProgress: (offset: number) => void;
    reset: () => void;
    pause: () => void;
};
export {};
