import type { InspectorProxyConfig } from '@granite-js/plugin-core';
import * as ws from 'ws';
export declare class DebuggerEventHandler {
    private delegate?;
    private networkResponseData;
    constructor(delegate?: InspectorProxyConfig['delegate']);
    setDeviceWebSocketHandler(socket: ws.WebSocket): void;
    setDebuggerWebSocketHandler(socket: ws.WebSocket): void;
    /**
     * 네이티브로부터 전달받은 wrappedEvent가 커스텀 이벤트인 경우 파싱하여 반환
     *
     * ```js
     * // message
     * {
     *   event: 'wrappedEvent',
     *   payload: {
     *     wrappedEvent: '<stringified event string>'
     *   }
     * }
     *
     * // wrappedEvent
     * {
     *   method: '<Namespace>.*',
     *   params: {}
     * }
     * ```
     */
    private safetyParseCustomEvent;
    /**
     * Chrome Devtools Frontend 로부터 전달받은 CDP 이벤트 요청 데이터인 경우 파싱하여 반환
     *
     * ```js
     * {
     *   method: 'CDP Event',
     *   params: {}
     * }
     * ```
     */
    private safetyParseDebuggerEvent;
    private handleDeviceMessage;
    private handleDebuggerMessage;
    private handleNetworkResponseData;
    private handleGetResponseBody;
}
