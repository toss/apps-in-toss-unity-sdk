import { BuildResult, type CompleteGraniteConfig } from '@granite-js/plugin-core';
interface CommonMetroBuildOptions {
    outfile?: string;
    dev?: boolean;
    minify?: boolean;
    platform: 'ios' | 'android';
}
export interface MetroBuildOptions extends CommonMetroBuildOptions {
    config: CompleteGraniteConfig;
}
export interface MetroBuildAllOptions {
    config: CompleteGraniteConfig;
    concurrency?: number;
}
export declare function build({ config, ...options }: MetroBuildOptions): Promise<BuildResult>;
export declare function buildAll(optionsList: CommonMetroBuildOptions[], { config, concurrency }: MetroBuildAllOptions): Promise<BuildResult[]>;
export {};
