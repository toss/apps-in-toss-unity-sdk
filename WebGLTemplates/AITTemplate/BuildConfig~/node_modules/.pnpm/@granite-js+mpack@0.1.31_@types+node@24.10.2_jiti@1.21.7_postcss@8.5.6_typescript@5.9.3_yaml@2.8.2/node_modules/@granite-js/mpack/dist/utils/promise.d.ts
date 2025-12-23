export declare class PromiseHandler<T> {
    revisionId: number;
    private _isDone;
    private task;
    private resolver;
    private rejector;
    constructor(revisionId: number);
    get isDone(): boolean;
    wait(): Promise<T>;
    done(result: T): void;
    abort(reason?: Error): void;
}
export declare function isFulfilled<T>(task: PromiseSettledResult<T>): task is PromiseFulfilledResult<T>;
export declare function isRejected<T>(task: PromiseSettledResult<T>): task is PromiseRejectedResult;
