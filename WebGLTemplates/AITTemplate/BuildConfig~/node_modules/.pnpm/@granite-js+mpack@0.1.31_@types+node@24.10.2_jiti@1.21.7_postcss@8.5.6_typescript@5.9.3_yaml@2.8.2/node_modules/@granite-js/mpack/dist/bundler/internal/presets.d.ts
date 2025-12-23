import { type BuildConfig } from '@granite-js/plugin-core';
import type { BundlerConfig } from '../../types';
export declare function getReactNativeSetupScripts({ rootDir }: {
    rootDir: string;
}): string[];
export declare function globalVariables({ dev }: {
    dev: boolean;
}): string;
export declare function defineGlobalVariables({ dev }: {
    dev: boolean;
}): {
    window: string;
    __DEV__: string;
    'process.env.NODE_ENV': string;
};
export declare function combineWithBaseBuildConfig(config: BundlerConfig, context: {
    rootDir: string;
    dev: boolean;
}): BuildConfig;
