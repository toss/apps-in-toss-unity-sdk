/**
 * @name usePrevious
 * @category Hooks
 * @kind function
 * @description
 * A Hook that returns the previous value.
 * @param {T} value - The value to return the previous value of.
 * @returns {T} - Returns the previous value.
 * @example
 * ```typescript
 *  const isVisible = useVisibility();
 *  const prevValue = usePrevious<boolean>(isVisible) ?? false;
 * ```
 */
export declare function usePrevious<T>(value: T): T;
