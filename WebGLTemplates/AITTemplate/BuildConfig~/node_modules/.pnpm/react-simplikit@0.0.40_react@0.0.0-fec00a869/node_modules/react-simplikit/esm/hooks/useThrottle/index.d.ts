type ThrottleOptions = {
    /**
     * An optional array specifying whether the function should be invoked on the leading edge, trailing edge, or both.
     * If `edges` includes "leading", the function will be invoked at the start of the delay period.
     * If `edges` includes "trailing", the function will be invoked at the end of the delay period.
     * If both "leading" and "trailing" are included, the function will be invoked at both the start and end of the delay period.
     * @default ["leading", "trailing"]
     */
    edges?: Array<'leading' | 'trailing'>;
};
type ThrottledFunction<F extends (...args: any[]) => void> = {
    (...args: Parameters<F>): void;
    cancel: () => void;
};
declare function throttle<F extends (...args: any[]) => void>(func: F, throttleMs: number, { edges }?: ThrottleOptions): ThrottledFunction<F>;

/**
 * @description
 * `useThrottle` is a React hook that creates a throttled version of a callback function.
 * This is useful for limiting the rate at which a function can be called,
 * such as when handling scroll or resize events.
 *
 * @template {(...args: any[]) => any} F - The type of the callback function.
 * @param {F} callback - The function to be throttled.
 * @param {number} wait - The number of milliseconds to throttle invocations to.
 * @param {{ edges?: Array<'leading' | 'trailing'> }} [options] - Options to control the behavior of the throttle.
 * @param {Array<'leading' | 'trailing'>} [options.edges=['leading', 'trailing']] - An optional array specifying whether the function should be invoked on the leading edge, trailing edge, or both.
 * @returns {F & { cancel: () => void }} - Returns the throttled function with a `cancel` method to cancel pending executions.
 *
 * @example
 * const throttledScroll = useThrottle(() => {
 *   console.log('Scroll event');
 * }, 200, { edges: ['leading', 'trailing'] });
 *
 * useEffect(() => {
 *   window.addEventListener('scroll', throttledScroll);
 *   return () => {
 *     window.removeEventListener('scroll', throttledScroll);
 *     throttledScroll.cancel();
 *   };
 * }, [throttledScroll]);
 */
declare function useThrottle<F extends (...args: any[]) => any>(callback: F, wait: number, options?: Parameters<typeof throttle>[2]): {
    (...args: any[]): void;
    cancel: () => void;
};

export { useThrottle };
