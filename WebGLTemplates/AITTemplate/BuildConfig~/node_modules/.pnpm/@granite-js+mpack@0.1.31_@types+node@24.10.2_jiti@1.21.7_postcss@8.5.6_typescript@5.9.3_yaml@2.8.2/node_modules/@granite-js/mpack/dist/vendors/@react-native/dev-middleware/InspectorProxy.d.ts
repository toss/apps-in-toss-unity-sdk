import { unstable_InspectorProxy } from '@react-native/dev-middleware';
import { FastifyReply, FastifyRequest, HookHandlerDoneFunction } from 'fastify';
import * as ws from 'ws';
import { Device } from './Device';
export declare class InspectorProxy extends unstable_InspectorProxy {
    constructor({ root, serverBaseUrl }: {
        root: string;
        serverBaseUrl: string;
    });
    /**
     * 커스텀 Device 를 사용하기 위해 `InspectorProxy.createWebSocketListeners` 를 재구성한 메소드
     */
    createWebSocketServers({ onDeviceWebSocketConnected, onDebuggerWebSocketConnected, }: {
        onDeviceWebSocketConnected: (socket: ws.WebSocket) => void;
        onDebuggerWebSocketConnected: (socket: ws.WebSocket) => void;
    }): {
        deviceSocketServer: ws.WebSocketServer;
        debuggerSocketServer: ws.WebSocketServer;
    };
    /**
     * Fastify 에서 사용할 수 있도록 `InspectorProxy.processRequest` 의 인터페이스를 재구성한 메소드
     */
    handleRequest(request: FastifyRequest, reply: FastifyReply, done: HookHandlerDoneFunction): void;
    /**
     * 토스 커스텀 디버거를 띄우기 위해 내부 devices 를 노출시켜야 함
     */
    getDevices(): Map<string, Device>;
    private sendJsonResponse;
    private createDeviceWebSocketServer;
    private createDebuggerWebSocketServer;
}
