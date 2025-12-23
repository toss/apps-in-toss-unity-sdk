type SerializableType = string | number | boolean | object | null | undefined;
declare class Logger {
    private static LEVEL_COLOR;
    trace(...messages: SerializableType[]): void;
    debug(...messages: SerializableType[]): void;
    info(...messages: SerializableType[]): void;
    log(...messages: SerializableType[]): void;
    warn(...messages: SerializableType[]): void;
    error(...messages: SerializableType[]): void;
    getTimestamp(): string;
    private stdout;
    private stderr;
    private stringify;
    private createLogString;
}
export declare const logger: Logger;
export {};
