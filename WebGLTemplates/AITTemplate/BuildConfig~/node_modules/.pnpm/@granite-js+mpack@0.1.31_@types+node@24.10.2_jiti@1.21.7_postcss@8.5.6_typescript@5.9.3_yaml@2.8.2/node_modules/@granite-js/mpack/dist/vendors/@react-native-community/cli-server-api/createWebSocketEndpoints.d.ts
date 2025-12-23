import { MessageBroadcaster } from '../../../server/types';
interface WebSocketEndpointOptions {
    broadcast: MessageBroadcaster;
}
/**
 * import('@react-native-community/cli-server-api').createDevServerMiddleware 의 경우
 * 불필요한 미들웨어 구성까지 포함시키고 있기 때문에 필요한 대상만 가져와서 사용
 */
export declare function createWebSocketEndpoints(options: WebSocketEndpointOptions): {
    debuggerProxySocket: {
        server: ws.Server;
        isDebuggerConnected: () => boolean;
    };
    eventsSocket: {
        server: WebSocketServer;
        reportEvent: (event: any) => void;
    };
    messageSocket: {
        server: WebSocketServer;
        broadcast: (method: string, params?: Record<string, any>) => void;
    };
};
export {};
