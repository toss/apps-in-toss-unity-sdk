import type { ClientLogEvent } from '../server/types';
export declare const clientLogger: (level: ClientLogEvent["level"], data: any[]) => void;
