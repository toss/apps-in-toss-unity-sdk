import { Plugin } from 'esbuild';
import { PluginOptions } from '../types';
interface TransformPluginOptions {
    transformSync?: (id: string, code: string) => string;
    transformAsync?: (id: string, code: string) => Promise<string>;
}
export declare function transformPlugin({ context, ...options }: PluginOptions<TransformPluginOptions>): Plugin;
export {};
