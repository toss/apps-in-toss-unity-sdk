import connect from 'connect';
interface DebuggerMiddlewareConfig {
    port: number;
    broadcastMessage: (method: string, params?: Record<string, unknown>) => void;
}
export declare function createDebuggerMiddleware({ port, broadcastMessage }: DebuggerMiddlewareConfig): {
    middleware: connect.Server;
    enableStdinWatchMode: () => void;
};
export {};
