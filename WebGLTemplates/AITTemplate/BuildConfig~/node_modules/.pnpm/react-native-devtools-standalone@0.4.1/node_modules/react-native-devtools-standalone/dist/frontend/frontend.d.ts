import { type Config as DevtoolsStoreConfig, type DevtoolsProps } from 'react-devtools-inline/frontend';
type Target = 'client' | 'proxy-server';
interface DevToolsConfigs {
    /**
     * Element to render DevTools.
     */
    element: HTMLElement;
    /**
     * Proxy web socket server host.
     *
     * Defaults to `'localhost'`
     */
    host?: string;
    /**
     * Proxy web socket server port.
     *
     * Defaults to `8098`
     */
    port?: number;
    /**
     * React DevTools store config.
     */
    devtoolsStoreConfig?: DevtoolsStoreConfig;
    /**
     * React DevTools props.
     *
     * Defaults to `{ showTabBar: true, hideViewSourceAction: true }`
     */
    devtoolsProps?: DevtoolsProps;
    /**
     * WebSocket delegate.
     */
    delegate?: ProxyWebSocketDelegate;
}
interface ProxyWebSocketDelegate {
    onConnect?: (context: {
        target: Target;
    }) => void;
    onClose?: (context: {
        target: Target;
    }) => boolean | void;
    onMessage?: (context: {
        data: string;
    }) => boolean | void;
    onSend?: (context: {
        data: string;
    }) => boolean | void;
}
export declare const setupDevTools: (config: DevToolsConfigs) => void;
export {};
