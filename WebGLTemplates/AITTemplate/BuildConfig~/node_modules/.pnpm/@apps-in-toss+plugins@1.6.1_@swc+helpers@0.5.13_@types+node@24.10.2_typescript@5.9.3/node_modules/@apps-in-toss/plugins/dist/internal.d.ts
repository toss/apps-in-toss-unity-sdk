import { GranitePluginCore } from '@granite-js/plugin-core';

interface AppsInTossHostOptions {
    remote?: Partial<{
        host: string;
        port: number;
    }>;
}
declare function appsInTossHost(options: AppsInTossHostOptions): (GranitePluginCore | Promise<GranitePluginCore>)[];

export { type AppsInTossHostOptions, appsInTossHost };
