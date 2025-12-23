import { type PerformanceMeasure, type MarkOptions } from 'perf_hooks';
export interface PerfStatic {
    trace: (name: string, options?: MarkOptions) => Perf;
    withTrace: <T>(task: () => T | Promise<T>, config: {
        name: string;
        startOptions?: MarkOptions;
        stopOptions?: (result: Awaited<T>) => MarkOptions;
    }) => Promise<T>;
    getSummary: () => Record<string, {
        averageDuration: number;
        records: PerfResult[];
    }> | null;
}
export interface Perf {
    stop: (options?: MarkOptions) => void;
}
export interface PerfResult {
    sequence: number;
    startTime: number;
    duration: number;
    detail?: PerformanceMeasure['detail'];
}
export declare const Performance: PerfStatic;
