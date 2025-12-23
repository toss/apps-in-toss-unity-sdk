import type { BuildConfig } from '@granite-js/plugin-core';
import * as esbuild from 'esbuild';
export declare function buildWithEsbuild(buildConfig: BuildConfig, options?: esbuild.BuildOptions): Promise<string>;
