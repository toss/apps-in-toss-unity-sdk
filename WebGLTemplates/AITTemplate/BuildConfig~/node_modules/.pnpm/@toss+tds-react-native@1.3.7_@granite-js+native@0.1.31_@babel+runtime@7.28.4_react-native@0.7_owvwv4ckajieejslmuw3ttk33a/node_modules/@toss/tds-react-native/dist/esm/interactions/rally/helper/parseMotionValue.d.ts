import type { RelativeOperator } from '../types';
type Unit = 'px' | '%' | 'deg' | 'number';
export declare function parseMotionValue(input: string | number): {
    operator: undefined;
    number: number;
    unit: string;
} | {
    operator: RelativeOperator;
    number: number;
    unit: Unit;
} | undefined;
export {};
