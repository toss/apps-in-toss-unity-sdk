import * as z from 'zod';
export type StackFrame = z.TypeOf<typeof stackFrame>;
declare const stackFrame: z.ZodObject<{
    file: z.ZodString;
    lineNumber: z.ZodNumber;
    column: z.ZodNumber;
    methodName: z.ZodString;
}, "strip", z.ZodTypeAny, {
    file: string;
    column: number;
    lineNumber: number;
    methodName: string;
}, {
    file: string;
    column: number;
    lineNumber: number;
    methodName: string;
}>;
export declare function parseStackFrame(body: unknown): {
    file: string;
    column: number;
    lineNumber: number;
    methodName: string;
}[] | null;
export {};
