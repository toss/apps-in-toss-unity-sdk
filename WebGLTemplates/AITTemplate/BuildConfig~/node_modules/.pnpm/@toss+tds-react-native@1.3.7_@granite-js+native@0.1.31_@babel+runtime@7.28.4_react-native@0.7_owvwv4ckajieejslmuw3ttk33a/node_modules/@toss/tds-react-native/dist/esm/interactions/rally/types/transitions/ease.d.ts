import type { Spring } from './spring';
export type BezierArray = [number, number, number, number];
export type EasingFn = (t: number) => number;
export type Easing = EasingFn | Spring | BezierArray;
