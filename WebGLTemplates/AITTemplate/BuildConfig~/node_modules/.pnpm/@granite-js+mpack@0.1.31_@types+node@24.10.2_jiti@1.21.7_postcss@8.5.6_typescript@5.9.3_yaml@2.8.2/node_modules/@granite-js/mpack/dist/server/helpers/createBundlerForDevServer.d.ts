import type { BuildConfig } from '@granite-js/plugin-core';
import { Bundler } from '../../bundler';
import { Platform } from '../types';
export declare function createBundlerForDevServer({ rootDir, platform, buildConfig, }: {
    rootDir: string;
    platform: Platform;
    buildConfig: Omit<BuildConfig, 'outfile' | 'platform'>;
}): Promise<Bundler>;
