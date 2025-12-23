import type { BuildResult } from '@granite-js/plugin-core';
import { BundlerConfig, INTERNAL__Id } from '../types';
import { Plugin } from '../types/Plugin';
export declare class Bundler {
    private config;
    private id;
    private revisionId;
    private status;
    private pluginDriver;
    private esbuildContext;
    private bundleTask;
    constructor(config: BundlerConfig);
    build(options?: {
        withDispose?: boolean;
    }): Promise<BuildResult>;
    getId(): INTERNAL__Id;
    addPlugin(plugin: Plugin): this;
    private setupUncaughtExceptionHandler;
    private getBaseBuildOptions;
    private setupEnvironment;
    private handlePrepare;
    private handleStart;
    private handleLoad;
    private handleEnd;
}
