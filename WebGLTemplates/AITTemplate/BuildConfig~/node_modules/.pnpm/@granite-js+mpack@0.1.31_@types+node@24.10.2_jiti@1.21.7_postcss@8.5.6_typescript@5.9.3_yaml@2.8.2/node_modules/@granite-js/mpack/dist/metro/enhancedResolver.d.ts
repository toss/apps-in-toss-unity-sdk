import type { MetroResolutionContext } from '@granite-js/plugin-core';
interface CreateResolverOptions {
    conditionNames?: string[];
}
export declare function createResolver(rootPath: string, options?: CreateResolverOptions): (context: MetroResolutionContext, request: string, platform: string | null) => any;
export {};
