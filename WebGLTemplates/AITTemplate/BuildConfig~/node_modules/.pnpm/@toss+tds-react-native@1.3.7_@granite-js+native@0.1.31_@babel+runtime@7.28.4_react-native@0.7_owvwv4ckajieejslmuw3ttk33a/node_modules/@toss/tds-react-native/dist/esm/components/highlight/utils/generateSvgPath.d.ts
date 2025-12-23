import type { Layout } from '../types';
export declare const generateRadiusRectanglePath: ({ x, y, width, height, radius }: Layout & {
    radius: number;
}) => string;
export declare const generateWindowPath: () => string;
export declare const generateOverlayPath: ({ width, height, x, y, radius }: Layout & {
    radius: number;
}) => string;
