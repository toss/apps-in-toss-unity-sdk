import { DependencyList, EffectCallback } from 'react';

/**
 * @description
 * `useConditionalEffect` is a React hook that conditionally executes effects based on a predicate function.
 * This provides more control over when effects run beyond just dependency changes.
 *
 * @param {EffectCallback} effect - The effect callback to run.
 * @param {DependencyList} deps - Dependencies array, similar to useEffect.
 * @param {(prevDeps: T | undefined, currentDeps: T) => boolean} condition - Function that determines if the effect should run based on previous and current deps.
 * - On the initial render, `prevDeps` will be `undefined`. Your `condition` function should handle this case.
 * - If you want your effect to run on the initial render, return `true` when `prevDeps` is `undefined`.
 * - If you don't want your effect to run on the initial render, return `false` when `prevDeps` is `undefined`.
 *
 * @example
 * import { useConditionalEffect } from 'react-simplikit';
 *
 * function Component() {
 *   const [count, setCount] = useState(0);
 *
 *   // Only run effect when count increases
 *   useConditionalEffect(
 *     () => {
 *       console.log(`Count increased to ${count}`);
 *     },
 *     [count],
 *     (prevDeps, currentDeps) => {
 *       // Only run when count is defined and has increased
 *       return prevDeps && currentDeps[0] > prevDeps[0];
 *     }
 *   );
 *
 *   return (
 *     <button onClick={() => setCount(prev => prev + 1)}>
 *       Increment: {count}
 *     </button>
 *   );
 * }
 *
 */
declare function useConditionalEffect<T extends DependencyList>(effect: EffectCallback, deps: T, condition: (prevDeps: T | undefined, currentDeps: T) => boolean): void;

export { useConditionalEffect };
