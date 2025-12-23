import type { TokenShadowProps } from './types';
export type ShadowTokenStrength = 'tiny' | 'weak' | 'medium';
export type ShadowTokenDirection = 'up' | 'down';
type ShadowToken = (direction: ShadowTokenDirection) => TokenShadowProps;
export declare const shadow: Record<ShadowTokenStrength, ShadowToken>;
export {};
