import { FastifyInstance } from 'fastify';
interface DebuggerPluginConfig {
    onReload: () => void;
}
declare function debuggerPluginImpl(app: FastifyInstance, config: DebuggerPluginConfig): Promise<void>;
export declare const debuggerPlugin: typeof debuggerPluginImpl;
export {};
