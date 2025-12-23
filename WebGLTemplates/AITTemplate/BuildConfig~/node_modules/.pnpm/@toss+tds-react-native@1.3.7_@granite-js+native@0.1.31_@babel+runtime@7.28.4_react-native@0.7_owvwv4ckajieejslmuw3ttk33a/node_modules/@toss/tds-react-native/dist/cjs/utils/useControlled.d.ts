export interface UseControlledProps<T> {
    controlledValue?: T;
    defaultValue: T;
}
export declare function useControlled<T = unknown>({ controlledValue, defaultValue, }: UseControlledProps<T>): [T, (newValue: T) => void];
