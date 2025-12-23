type DebounceOptions = {
    leading?: boolean;
    trailing?: boolean;
};
/**
 * @description
 * `useDebouncedCallback` is a React hook that returns a debounced version of the provided callback function.
 * It helps optimize event handling by delaying function execution and grouping multiple calls into one.
 *
 * Note that if both 'leading' and 'trailing' are set, the function will be called at both the start and end of the delay period. However, it must be called at least twice within debounceMs interval for this to happen, since one debounced function call cannot trigger the function twice.
 *
 * @param {Object} options - The options object.
 * @param {Function} options.onChange - The callback function to debounce.
 * @param {number} options.timeThreshold - The number of milliseconds to delay the function execution.
 * @param {boolean} [options.leading=false] - If `true`, the function is called at the start of the sequence.
 * @param {boolean} [options.trailing=true] - If `true`, the function is called at the end of the sequence.
 *
 * @returns {Function} A debounced function that delays invoking the callback.
 *
 * @example
 * function SearchInput() {
 *   const [query, setQuery] = useState('');
 *   const debouncedSetQuery = useDebouncedCallback({ onChange: setQuery, timeThreshold: 100 });
 *   return <input type="text" onChange={(e) => debouncedSetQuery(e.target.value)} />;
 * }
 */
declare function useDebouncedCallback({ onChange, timeThreshold, leading, trailing, }: DebounceOptions & {
    onChange: (newValue: boolean) => void;
    timeThreshold: number;
}): (nextValue: boolean) => void;

export { useDebouncedCallback };
