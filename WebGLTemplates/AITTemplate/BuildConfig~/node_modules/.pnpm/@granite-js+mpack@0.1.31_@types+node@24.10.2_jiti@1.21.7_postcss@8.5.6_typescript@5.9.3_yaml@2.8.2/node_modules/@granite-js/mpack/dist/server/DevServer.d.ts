import type { BroadcastCommand, DevServerOptions } from './types';
import { InspectorProxy } from '../vendors/@react-native/dev-middleware';
export declare class DevServer {
    private devServerOptions;
    host: string;
    port: number;
    private app;
    private context;
    private inspectorProxy?;
    private wssDelegate?;
    constructor(devServerOptions: DevServerOptions);
    initialize(): Promise<void>;
    listen(): Promise<void>;
    close(): Promise<undefined>;
    getInspectorProxy(): InspectorProxy | undefined;
    getBaseUrl(): string;
    broadcastCommand(command: BroadcastCommand): void;
    private getContext;
    private setup;
    private setCommonHeaders;
    private createDevServerContext;
    private getBundle;
}
declare global {
    var remoteBundles: Record<'android' | 'ios', string> | null;
}
