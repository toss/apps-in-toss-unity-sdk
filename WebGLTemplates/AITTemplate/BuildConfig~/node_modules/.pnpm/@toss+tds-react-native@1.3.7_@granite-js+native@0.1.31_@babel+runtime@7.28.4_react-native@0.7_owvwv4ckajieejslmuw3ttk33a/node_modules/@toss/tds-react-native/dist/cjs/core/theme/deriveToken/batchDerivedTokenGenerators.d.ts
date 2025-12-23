import type { SeedToken } from '../seedToken';
import type { BaseDerivedTokenGenerator } from './BaseDerivedTokenGenerator';
type DerivedTokenGenerators = Record<string, BaseDerivedTokenGenerator<unknown>>;
export declare function batchDerivedTokenGenerators<T extends DerivedTokenGenerators>(styleMap: T): (seed: SeedToken) => { [K in keyof T]: T[K] extends BaseDerivedTokenGenerator<infer R> ? R : never; };
export {};
