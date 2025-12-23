import { FastifyInstance } from 'fastify';
interface StatusPluginConfig {
    rootDir: string;
}
declare function _statusPlugin(app: FastifyInstance, config: StatusPluginConfig): Promise<void>;
export declare const statusPlugin: typeof _statusPlugin;
export {};
