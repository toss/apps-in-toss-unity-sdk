import { BroadcastCommand, ClientLogEvent, MessageBroadcaster } from '../../server/types';
interface WebSocketServerDelegateParams {
    eventReporter: (event: ClientLogEvent) => void;
    messageBroadcaster: MessageBroadcaster;
    hmr: {
        updateStart: () => void;
        updateDone: () => void;
        reload: () => void;
    };
}
export declare class WebSocketServerDelegate {
    private delegateParams;
    constructor(delegateParams: WebSocketServerDelegateParams);
    sendEvent(event: ClientLogEvent): void;
    broadcastCommand(command: BroadcastCommand, params?: Record<string, unknown>): void;
    onHMRUpdateStart(): void;
    onHMRUpdateDone(): void;
    /**
     * @TODO: HMR 구현 필요 (대신 실시간 새로고침 제공)
     */
    hotReload(): void;
}
export {};
