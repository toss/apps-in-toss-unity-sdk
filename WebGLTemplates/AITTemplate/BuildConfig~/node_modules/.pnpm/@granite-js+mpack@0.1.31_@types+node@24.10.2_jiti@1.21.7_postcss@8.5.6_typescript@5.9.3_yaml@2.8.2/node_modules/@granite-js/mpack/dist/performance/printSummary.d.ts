import { PerfStatic } from './Performance';
export interface PrintSummaryOptions {
    limit?: number;
}
export declare function printSummary(summary: NonNullable<ReturnType<PerfStatic['getSummary']>>, options?: PrintSummaryOptions): void;
