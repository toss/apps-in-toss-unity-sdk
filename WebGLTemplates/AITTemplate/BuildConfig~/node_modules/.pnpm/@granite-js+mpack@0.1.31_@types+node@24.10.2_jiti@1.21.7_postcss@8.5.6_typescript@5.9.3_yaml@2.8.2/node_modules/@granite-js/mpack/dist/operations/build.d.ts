import { type BuildConfig, type CompleteGraniteConfig } from '@granite-js/plugin-core';
import type { BundlerConfig, PluginFactory } from '../types';
type CommonBuildOptions = Omit<BundlerConfig, 'rootDir' | 'buildConfig'> & Pick<BuildConfig, 'platform' | 'outfile'>;
export interface BuildOptions extends CommonBuildOptions {
    config: CompleteGraniteConfig;
    plugins?: PluginFactory[];
}
export interface BuildAllOptions {
    config: CompleteGraniteConfig;
    concurrency?: number;
    plugins?: PluginFactory[];
}
export declare function build({ config, plugins, ...options }: BuildOptions): Promise<import("@granite-js/plugin-core").BuildSuccessResult>;
export declare function buildAll(optionsList: CommonBuildOptions[], { config, plugins, concurrency }: BuildAllOptions): Promise<import("@granite-js/plugin-core").BuildSuccessResult[]>;
export {};
