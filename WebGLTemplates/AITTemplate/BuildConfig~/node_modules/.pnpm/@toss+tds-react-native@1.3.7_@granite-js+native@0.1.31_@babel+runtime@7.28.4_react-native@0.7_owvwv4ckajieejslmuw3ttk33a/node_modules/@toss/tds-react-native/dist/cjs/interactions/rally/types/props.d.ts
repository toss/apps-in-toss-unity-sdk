import type { MotionInput } from './motion-input/motion-input';
import type { Easing } from './transitions';
export type RallyPlayConfig = {
    playCount?: number | 'infinite';
    /**
     * @default alternate
     */
    loopMode?: 'alternate' | 'normal';
    loopDelay?: number;
    delay?: number;
};
export type RallyProps = {
    motion: MotionInput | MotionInput[];
    exit?: MotionInput;
    defaultEasing?: Easing;
    defaultDuration?: number;
} & RallyPlayConfig;
