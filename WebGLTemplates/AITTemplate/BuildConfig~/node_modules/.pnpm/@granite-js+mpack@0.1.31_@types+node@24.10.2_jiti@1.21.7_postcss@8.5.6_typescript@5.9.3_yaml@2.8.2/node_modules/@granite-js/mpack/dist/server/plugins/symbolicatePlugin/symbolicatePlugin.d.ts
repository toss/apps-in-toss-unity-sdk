import type { BundleData } from '@granite-js/plugin-core';
import type { FastifyInstance } from 'fastify';
import type { Platform } from '../../types';
declare function symbolicatePluginImpl(app: FastifyInstance, { getBundle }: {
    getBundle: (platform: Platform) => Promise<BundleData>;
}): Promise<void>;
export declare const symbolicatePlugin: typeof symbolicatePluginImpl;
export {};
