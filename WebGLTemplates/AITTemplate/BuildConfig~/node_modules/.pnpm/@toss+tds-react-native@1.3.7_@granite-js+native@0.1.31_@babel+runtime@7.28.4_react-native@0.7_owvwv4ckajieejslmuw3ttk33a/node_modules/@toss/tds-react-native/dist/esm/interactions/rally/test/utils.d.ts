import type { Layout } from '../types';
export declare const MOCK_LAYOUT: Layout;
export declare function toTestMotion(motion: any, playConfig?: {
    delay: number;
    duration: undefined;
    easing: undefined;
}): {
    [k: string]: any;
};
export declare function toTestMotions(motions: any[], playConfig?: {
    delay: number;
    duration: undefined;
    easing: undefined;
}): {
    [k: string]: any;
}[];
