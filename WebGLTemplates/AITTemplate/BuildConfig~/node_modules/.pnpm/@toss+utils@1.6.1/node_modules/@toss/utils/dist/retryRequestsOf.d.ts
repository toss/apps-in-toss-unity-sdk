/** @tossdocs-ignore */
interface RetryOptions {
    retries: number;
    shouldRetry?: (error: Error) => boolean;
    onError?: (error: Error) => void | Promise<void>;
}
export declare function retryRequestsOf<Arguments extends any[], Result>(task: (...args: Arguments) => Result | Promise<Result>, { retries, shouldRetry, onError }: RetryOptions): (...args: Arguments) => Promise<Result>;
export {};
