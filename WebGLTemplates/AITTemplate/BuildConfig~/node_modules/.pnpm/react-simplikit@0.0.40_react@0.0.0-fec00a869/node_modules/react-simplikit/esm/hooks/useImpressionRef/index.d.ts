type UseImpressionRefOptions = Partial<{
    onImpressionStart: () => void;
    onImpressionEnd: () => void;
    rootMargin: string;
    areaThreshold: number;
    timeThreshold: number;
}>;
/**
 * @description
 * `useImpressionRef` is a React hook that measures the time a specific DOM element is visible on the screen and executes callbacks when the element enters or exits the viewport.
 * It uses `IntersectionObserver` and the `Visibility API` to track the element's visibility.
 *
 * @param {UseImpressionRefOptions} options - Options for tracking the element's visibility.
 * @param {() => void} [options.onImpressionStart] - Callback function executed when the element enters the view
 * @param {() => void} [options.onImpressionEnd] - Callback function executed when the element exits the view
 * @param {number} [options.timeThreshold=0] - Minimum time the element must be visible (in milliseconds)
 * @param {number} [options.areaThreshold=0] - Minimum ratio of the element that must be visible (0 to 1)
 * @param {string} options.rootMargin - Margin to adjust the detection area
 *
 * @returns {(element: Element | null) => void} A function to set the element. Attach this function to the `ref` attribute, and the callbacks will be executed whenever the element's visibility changes.
 *
 * @example
 * import { useImpressionRef } from 'react-simplikit';
 *
 * function Component() {
 *   const ref = useImpressionRef<HTMLDivElement>({
 *     onImpressionStart: () => console.log('Element entered view'),
 *     onImpressionEnd: () => console.log('Element exited view'),
 *     timeThreshold: 1000,
 *     areaThreshold: 0.5,
 *   });
 *
 *   return <div ref={ref}>Track my visibility!</div>;
 * }
 */
declare function useImpressionRef<Element extends HTMLElement>({ onImpressionStart, onImpressionEnd, rootMargin, areaThreshold, timeThreshold, }: UseImpressionRefOptions): (element: Element | null) => void;

export { type UseImpressionRefOptions, useImpressionRef };
