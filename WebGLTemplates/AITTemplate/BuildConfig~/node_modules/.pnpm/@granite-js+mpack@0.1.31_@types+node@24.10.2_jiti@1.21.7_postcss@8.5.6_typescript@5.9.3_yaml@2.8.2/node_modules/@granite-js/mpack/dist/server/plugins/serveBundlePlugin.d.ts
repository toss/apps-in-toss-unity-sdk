import type { BundleData } from '@granite-js/plugin-core';
import type { FastifyInstance } from 'fastify';
import type { Platform } from '../types';
declare function _serveBundlePlugin(app: FastifyInstance, { getBundle }: {
    getBundle: (platform: Platform) => Promise<BundleData>;
}): Promise<void>;
export declare const serveBundlePlugin: typeof _serveBundlePlugin;
export {};
