import type { SeedToken } from '../seedToken';
export declare abstract class BaseDerivedTokenGenerator<T> {
    private cache;
    create(seed: SeedToken): T;
    protected abstract calculate(seed: SeedToken): T;
}
