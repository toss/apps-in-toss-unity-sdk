type IntervalOptions = number | {
    delay: number;
    immediate?: boolean;
    enabled?: boolean;
};
/**
 * @description
 * `useInterval` is a React hook that executes a function at a specified interval.
 * It is useful for timers, polling data, and other recurring tasks.
 *
 * @param {() => void} callback - The function to be executed periodically.
 * @param {number | { delay: number; enabled?: boolean; immediate?: boolean }} options - Configures the interval behavior.
 * @param {number} options.delay - The interval duration in milliseconds. If `null`, the interval will not run.
 * @param {boolean} [options.immediate=false] - If `true`, executes immediately before starting the interval.
 * @param {boolean} [options.enabled=true] - If `false`, the interval will not run.
 *
 * @example
 * import { useInterval } from 'react-simplikit';
 * import { useState } from 'react';
 *
 * function Timer() {
 *   const [time, setTime] = useState(0);
 *
 *   useInterval(() => {
 *     setTime(prev => prev + 1);
 *   }, 1000);
 *
 *   return (
 *     <div>
 *       <p>{time} seconds</p>
 *     </div>
 *   );
 * }
 */
declare function useInterval(callback: () => void, options: IntervalOptions): void;

export { useInterval };
