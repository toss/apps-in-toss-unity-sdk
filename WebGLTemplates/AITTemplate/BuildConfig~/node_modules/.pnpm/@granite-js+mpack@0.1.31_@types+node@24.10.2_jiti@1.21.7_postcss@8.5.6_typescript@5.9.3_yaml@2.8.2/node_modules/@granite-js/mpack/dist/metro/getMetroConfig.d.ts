import type { AdditionalMetroConfig } from '@granite-js/plugin-core';
export interface GetMetroConfig {
    rootPath: string;
}
export declare function getMetroConfig({ rootPath }: GetMetroConfig, additionalConfig?: AdditionalMetroConfig): Promise<any>;
