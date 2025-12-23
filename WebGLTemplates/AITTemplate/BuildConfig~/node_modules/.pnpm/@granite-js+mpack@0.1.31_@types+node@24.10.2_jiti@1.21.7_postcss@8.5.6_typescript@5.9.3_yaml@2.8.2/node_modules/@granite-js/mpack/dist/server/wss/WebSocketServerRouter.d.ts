import { FastifyInstance } from 'fastify';
import * as ws from 'ws';
export declare class WebSocketServerRouter {
    private webSocketServers;
    register(path: string, wss: ws.WebSocketServer): this;
    setup(app: FastifyInstance): void;
    private onUpgrade;
}
