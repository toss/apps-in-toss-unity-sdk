import type { BundleData } from '@granite-js/plugin-core';
import { Platform } from '../types';
export declare function mergeBundles({ platform, hostBundleContent, remoteBundleContent, }: {
    platform: Platform;
    hostBundleContent: string;
    remoteBundleContent: string;
}): Promise<BundleData>;
