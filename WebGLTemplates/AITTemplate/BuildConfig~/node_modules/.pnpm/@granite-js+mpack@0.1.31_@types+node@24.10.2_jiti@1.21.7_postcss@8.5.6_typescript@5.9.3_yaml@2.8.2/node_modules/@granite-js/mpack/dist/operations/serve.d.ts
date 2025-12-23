import { type CompleteGraniteConfig } from '@granite-js/plugin-core';
interface RunServerConfig {
    config: CompleteGraniteConfig;
    host?: string;
    port?: number;
    enableEmbeddedReactDevTools?: boolean;
    onServerReady?: () => Promise<void> | void;
}
export declare function runServer({ config, host, port, enableEmbeddedReactDevTools, onServerReady, }: RunServerConfig): Promise<void>;
export {};
