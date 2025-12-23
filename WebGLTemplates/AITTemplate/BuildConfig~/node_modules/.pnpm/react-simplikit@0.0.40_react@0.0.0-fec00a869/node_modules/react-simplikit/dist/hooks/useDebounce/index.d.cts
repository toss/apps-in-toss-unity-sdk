type DebouncedFunction<F extends (...args: any[]) => void> = {
    (...args: Parameters<F>): void;
    cancel: () => void;
};

type DebounceOptions = {
    leading?: boolean;
    trailing?: boolean;
};
/**
 * @description
 * `useDebounce` is a React hook that returns a debounced version of the provided callback function.
 * It helps optimize event handling by delaying function execution and grouping multiple calls into one.
 *
 * @template {(...args: any[]) => unknown} F - The type of the callback function.
 * @param {F} callback - The function to debounce.
 * @param {number} wait - The number of milliseconds to delay the function execution.
 * @param {DebounceOptions} [options] - Configuration options for debounce behavior.
 * @param {boolean} [options.leading=false] - If `true`, the function is called at the start of the sequence.
 * @param {boolean} [options.trailing=true] - If `true`, the function is called at the end of the sequence.
 *
 * @returns {F & { cancel: () => void }} A debounced function that delays invoking the callback.
 *   It also includes a `cancel` method to cancel any pending debounced execution.
 *
 * @example
 * function SearchInput() {
 *   const [query, setQuery] = useState('');
 *
 *   const debouncedSearch = useDebounce((value: string) => {
 *     // Actual API call
 *     searchAPI(value);
 *   }, 300);
 *
 *   return (
 *     <input
 *       value={query}
 *       onChange={e => {
 *         setQuery(e.target.value);
 *         debouncedSearch(e.target.value);
 *       }}
 *       placeholder="Enter search term"
 *     />
 *   );
 * }
 */
declare function useDebounce<F extends (...args: any[]) => unknown>(callback: F, wait: number, options?: DebounceOptions): DebouncedFunction<F>;

export { useDebounce };
