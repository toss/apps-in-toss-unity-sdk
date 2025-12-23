import type { Rgb } from 'culori';
export interface Oklch {
    mode: 'oklch';
    l: number;
    c: number;
    h: number;
    alpha: number;
}
export declare class ColorManager {
    private color;
    private alpha;
    constructor(inputColor: string);
    setLightness(l: number): this;
    setAlpha(alpha: number): this;
    getOklch(): Oklch;
    getHue(): number;
    getChroma(): number;
    getLightness(): number;
    getAlpha(): number;
    getHex(): string;
    getRgba(): string;
}
export declare const serializeRgba: (color: Partial<Rgb>) => string;
