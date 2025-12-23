import { CompleteGraniteConfig } from '@granite-js/plugin-core';
interface RunServerArgs {
    config: CompleteGraniteConfig;
    host?: string;
    port?: number;
    onServerReady?: () => Promise<void> | void;
}
export declare function EXPERIMENTAL__server({ config, host, port, onServerReady, }: RunServerArgs): Promise<{
    cleanup: () => Promise<void>;
}>;
export {};
