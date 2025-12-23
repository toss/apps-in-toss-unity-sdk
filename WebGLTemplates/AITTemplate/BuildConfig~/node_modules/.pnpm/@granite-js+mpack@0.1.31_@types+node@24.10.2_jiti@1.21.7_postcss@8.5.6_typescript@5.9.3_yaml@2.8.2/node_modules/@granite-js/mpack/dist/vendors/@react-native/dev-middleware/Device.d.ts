import { unstable_Device } from '@react-native/dev-middleware';
import * as ws from 'ws';
export declare class Device extends unstable_Device {
    constructor(id: string, name: string, app: string, socket: ws.WebSocket, projectRoot: string);
}
