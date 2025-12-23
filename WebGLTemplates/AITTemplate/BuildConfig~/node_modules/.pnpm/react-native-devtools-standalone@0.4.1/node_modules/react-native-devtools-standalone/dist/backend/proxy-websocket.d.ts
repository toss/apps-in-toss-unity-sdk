import * as ws from 'ws';
interface ProxyWebSocketOptions {
    host?: string;
    port: number;
    delegate?: ProxyWebSocketDelegate;
}
export interface ProxyWebSocketDelegate {
    onConnect?: (context: ProxyWebSocketDelegateContext<{
        socket: ws.WebSocket;
    }>) => boolean | void;
    onClose?: (context: ProxyWebSocketDelegateContext) => boolean | void;
    onMessage?: (context: ProxyWebSocketDelegateContext<{
        data: string;
    }>) => boolean | void;
    onError?: (error: Error) => void;
}
type ProxyWebSocketDelegateContext<T = object> = T & {
    proxyWebSocket: ProxyWebSocket | undefined;
};
export declare class ProxyWebSocket {
    private wss;
    private proxyWebSocket?;
    private delegate?;
    constructor({ host, port, delegate }: ProxyWebSocketOptions);
    private createProxyEvent;
    protected onConnect(socket: ws.WebSocket): void;
    protected onClose(): void;
    protected onMessage(data: ws.RawData): void;
    send(data: string): void;
    close(): Promise<void>;
    bind(proxyWebSocket: ProxyWebSocket): void;
    unbind(): void;
}
export {};
