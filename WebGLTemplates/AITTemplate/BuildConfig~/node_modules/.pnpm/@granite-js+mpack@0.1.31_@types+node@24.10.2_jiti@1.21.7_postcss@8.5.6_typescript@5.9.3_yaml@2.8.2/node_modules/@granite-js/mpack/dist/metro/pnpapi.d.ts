import type { getPackageInformation, topLevel } from 'pnpapi';
export interface PnpApi {
    getPackageInformation: typeof getPackageInformation;
    topLevel: typeof topLevel;
}
export declare let pnpapi: PnpApi | undefined;
