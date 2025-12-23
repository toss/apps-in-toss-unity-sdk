import * as ws from 'ws';
import { ClientLogEvent } from '../types';
export declare const createLiveReloadMiddleware: ({ onClientLog }: {
    onClientLog?: (event: ClientLogEvent) => void;
}) => {
    server: ws.Server<typeof ws.WebSocket, typeof import("http").IncomingMessage>;
    liveReload: () => void;
    updateStart: () => void;
    updateDone: () => void;
};
