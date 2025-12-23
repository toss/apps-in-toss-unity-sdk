export declare const RN_WSS_PORT = 8097;
export declare const DEFAULT_PROXY_WSS_PORT = 8098;
export declare const DEFAULT_HOST = "localhost";
export declare enum ProxyEventType {
    OPEN = "open",
    CLOSE = "close"
}
export interface ProxyEvent {
    event: ProxyEventType;
    __isProxy: boolean;
}
