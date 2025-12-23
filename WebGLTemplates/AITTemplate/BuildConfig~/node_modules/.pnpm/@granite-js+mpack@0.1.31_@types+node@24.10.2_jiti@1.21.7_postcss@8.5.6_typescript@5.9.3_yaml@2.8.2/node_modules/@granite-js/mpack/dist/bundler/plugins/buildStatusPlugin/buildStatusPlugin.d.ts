import type { BuildResult } from '@granite-js/plugin-core';
import * as esbuild from 'esbuild';
import type { PluginOptions } from '../types';
export interface BuildStatusPluginOptions {
    onPrepare: () => void | Promise<void>;
    onStart: () => void | Promise<void>;
    onLoad: (loadData: {
        moduleCount: number;
    }) => void | Promise<void>;
    onEnd: (buildResult: BuildResult) => void | Promise<void>;
}
export declare function buildStatusPlugin({ context, ...hooks }: PluginOptions<BuildStatusPluginOptions>): esbuild.Plugin;
